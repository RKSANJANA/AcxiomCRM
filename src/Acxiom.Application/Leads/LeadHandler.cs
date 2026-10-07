using System.Linq.Expressions;
using Acxiom.Application.Common;
using Acxiom.Application.Customers;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Leads;

public class LeadFilter : ListFilter
{
    public LeadStatus? Status { get; set; }
    public LeadSource? Source { get; set; }
}

public class LeadRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; }
    public LeadPriority Priority { get; set; }
    public decimal ExpectedValue { get; set; }
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class LeadView : LeadRow
{
    public string? Notes { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public int? CustomerId { get; set; }
    public int? OpportunityId { get; set; }
    public IReadOnlyList<LeadStatus> NextStatuses { get; set; } = Array.Empty<LeadStatus>();
    public bool CanConvert => Status == LeadStatus.Qualified;
    public bool IsClosed => Status is LeadStatus.Converted or LeadStatus.Lost;
}

public record ConvertLead(CustomerDetails Customer, string? DealName, decimal Amount, int Probability, DateTime CloseDate);

public record ConvertResult(int CustomerId, int OpportunityId, bool NewCustomer);

public class LeadHandler
{
    private static readonly Dictionary<string, Expression<Func<Lead, object?>>> SortColumns = new()
    {
        ["name"] = l => l.Name,
        ["company"] = l => l.Company,
        ["source"] = l => l.Source,
        ["status"] = l => l.Status,
        ["priority"] = l => l.Priority,
        ["value"] = l => l.ExpectedValue,
        ["created"] = l => l.CreatedAt,
    };

    private readonly IAppDbContext _db;
    private readonly Owners _owners;
    private readonly CustomerHandler _customers;
    private readonly IAuditLog _audit;
    private readonly ICurrentUser _me;
    private readonly TimeProvider _clock;

    public LeadHandler(IAppDbContext db, Owners owners, CustomerHandler customers, IAuditLog audit, ICurrentUser me, TimeProvider clock)
    {
        _db = db;
        _owners = owners;
        _customers = customers;
        _audit = audit;
        _me = me;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    public async Task<Paged<LeadRow>> SearchAsync(LeadFilter f)
    {
        var text = f.Text;
        var page = await _db.Leads.AsNoTracking()
            .WhereIf(text != null, l => l.Name.ToLower().Contains(text!) || l.Code.ToLower().Contains(text!)
                                        || (l.Company != null && l.Company.ToLower().Contains(text!))
                                        || (l.Email != null && l.Email.Contains(text!)))
            .WhereIf(f.Status != null, l => l.Status == f.Status)
            .WhereIf(f.Source != null, l => l.Source == f.Source)
            .WhereIf(!string.IsNullOrEmpty(f.Owner), l => l.OwnerId == f.Owner)
            .SortBy(f.Sort, SortColumns, "-created")
            .Select(l => new LeadRow
            {
                Id = l.Id, Code = l.Code, Name = l.Name, Company = l.Company, Email = l.Email, Phone = l.Phone, Source = l.Source,
                Status = l.Status, Priority = l.Priority, ExpectedValue = l.ExpectedValue, OwnerId = l.OwnerId, CreatedAt = l.CreatedAt
            }).PageAsync(f);

        await _owners.FillNamesAsync(page.Items, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return page;
    }

    public async Task<LeadView> GetAsync(int id)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Lead");
        var view = new LeadView
        {
            Id = lead.Id, Code = lead.Code, Name = lead.Name, Company = lead.Company, Email = lead.Email, Phone = lead.Phone,
            Source = lead.Source, Status = lead.Status, Priority = lead.Priority, ExpectedValue = lead.ExpectedValue,
            OwnerId = lead.OwnerId, CreatedAt = lead.CreatedAt, Notes = lead.Notes, ConvertedAt = lead.ConvertedAt,
            CustomerId = lead.CustomerId, OpportunityId = lead.OpportunityId, NextStatuses = lead.AllowedStatuses()
        };
        await _owners.FillNamesAsync(new[] { view }, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return view;
    }

    public async Task<int> CreateAsync(LeadDetails details, LeadStatus status, string? ownerId)
    {
        var owner = await _owners.PickAsync(ownerId);
        var count = await _db.Leads.IgnoreQueryFilters().CountAsync();
        var lead = Lead.Create($"LEAD-{count + 1:D4}", details, status, owner);
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return lead.Id;
    }

    public async Task UpdateAsync(int id, LeadDetails details, LeadStatus status, string? ownerId)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Lead");
        lead.Update(details);
        lead.MoveTo(status);
        lead.AssignTo(await _owners.PickAsync(ownerId, lead.OwnerId));
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        _owners.EnsureCanDelete();
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Lead");
        lead.Delete();
        await _db.SaveChangesAsync();
    }

    public async Task<ConvertLead> ConvertDraftAsync(int id)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Lead");
        return new ConvertLead(
            new CustomerDetails(lead.Name, lead.Email, lead.Phone, lead.Company, null, null, null, CustomerStatus.Active, null),
            $"{lead.Company ?? lead.Name} deal", lead.ExpectedValue, 25, Now.Date.AddDays(30));
    }

    public async Task<ConvertResult> ConvertAsync(int id, ConvertLead input)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Lead");
        if (!lead.CanConvert) throw new DomainException("Only qualified leads can be converted.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        // reuse a customer with the same email/phone if the user can see it
        var email = Rules.Email(input.Customer.Email);
        var phone = Rules.Phone(input.Customer.Phone);
        var existing = await _db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => !c.IsDeleted && (c.Email == email || c.Phone == phone));
        if (existing != null && !_me.CanSee(existing.OwnerId))
            throw new ConflictException($"{existing.Code} already uses this email or phone and belongs to another team.", "Email");

        var customer = existing;
        if (customer == null)
        {
            customer = Customer.Create(await _customers.NextCodeAsync(), input.Customer, lead.OwnerId);
            await _customers.EnsureNotDuplicateAsync(customer, 0);
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        var deal = Opportunity.Open(
            new OpportunityDetails(input.DealName, customer.Id, input.Amount, input.Probability, input.CloseDate, lead.Source, $"From lead {lead.Code}"),
            Stage.Qualification, lead.OwnerId, Now.Date, lead.Id);
        _db.Opportunities.Add(deal);
        await _db.SaveChangesAsync();

        lead.MarkConverted(customer.Id, deal.Id, Now);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _audit.WriteAsync("Convert", "Lead", lead.Id.ToString(),
            $"{lead.Code} -> customer {customer.Code}{(existing == null ? " (new)" : "")}, opportunity #{deal.Id}");
        return new ConvertResult(customer.Id, deal.Id, existing == null);
    }

    public async Task<List<Option>> OptionsAsync(bool openOnly)
    {
        var q = _db.Leads.AsNoTracking();
        if (openOnly) q = q.Where(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);
        return await q.OrderBy(l => l.Name).Select(l => new Option(l.Id, l.Name + " - " + l.Code)).ToListAsync();
    }
}
