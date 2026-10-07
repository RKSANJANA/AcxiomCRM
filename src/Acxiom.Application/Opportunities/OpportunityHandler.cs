using System.Linq.Expressions;
using Acxiom.Application.Common;
using Acxiom.Application.Customers;
using Acxiom.Domain.Common;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Opportunities;

public class OpportunityFilter : ListFilter
{
    public Stage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public int? CustomerId { get; set; }
}

public class OpportunityRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public Stage Stage { get; set; }
    public OpportunityStatus Status { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal Weighted => decimal.Round(Amount * Probability / 100m, 2);
}

public class OpportunityView : OpportunityRow
{
    public int? LeadId { get; set; }
    public LeadSource? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public record StageTotal(Stage Stage, int Deals, decimal Amount);
public record OwnerTotal(string OwnerId, string Owner, int OpenDeals, decimal Amount, decimal Weighted);
public record PipelineSummary(decimal OpenAmount, decimal Weighted, IReadOnlyList<StageTotal> ByStage, IReadOnlyList<OwnerTotal> ByOwner);

public class OpportunityHandler
{
    private static readonly Dictionary<string, Expression<Func<Opportunity, object?>>> SortColumns = new()
    {
        ["name"] = o => o.Name,
        ["amount"] = o => o.Amount,
        ["probability"] = o => o.Probability,
        ["stage"] = o => o.Stage,
        ["close"] = o => o.ExpectedCloseDate,
        ["created"] = o => o.CreatedAt,
    };

    private readonly IAppDbContext _db;
    private readonly Owners _owners;
    private readonly TimeProvider _clock;

    public OpportunityHandler(IAppDbContext db, Owners owners, TimeProvider clock)
    {
        _db = db;
        _owners = owners;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    public async Task<Paged<OpportunityRow>> SearchAsync(OpportunityFilter f)
    {
        var text = f.Text;
        var page = await Rows(_db.Opportunities.AsNoTracking()
                .WhereIf(text != null, o => o.Name.ToLower().Contains(text!)
                                            || _db.Customers.IgnoreQueryFilters().Any(c => c.Id == o.CustomerId && c.Name.ToLower().Contains(text!)))
                .WhereIf(f.Stage != null, o => o.Stage == f.Stage)
                .WhereIf(f.Status != null, o => o.Status == f.Status)
                .WhereIf(f.CustomerId != null, o => o.CustomerId == f.CustomerId)
                .WhereIf(!string.IsNullOrEmpty(f.Owner), o => o.OwnerId == f.Owner)
                .SortBy(f.Sort, SortColumns, "-created"))
            .PageAsync(f);

        await _owners.FillNamesAsync(page.Items, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return page;
    }

    /// <summary>All open deals for the pipeline board.</summary>
    public async Task<List<OpportunityRow>> BoardAsync(string? owner)
    {
        var rows = await Rows(_db.Opportunities.AsNoTracking()
                .Where(o => o.Status == OpportunityStatus.Open)
                .WhereIf(!string.IsNullOrEmpty(owner), o => o.OwnerId == owner)
                .OrderBy(o => o.ExpectedCloseDate))
            .ToListAsync();
        await _owners.FillNamesAsync(rows, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return rows;
    }

    public async Task<OpportunityView> GetAsync(int id)
    {
        var view = await _db.Opportunities.AsNoTracking().Where(o => o.Id == id).Select(o => new OpportunityView
        {
            Id = o.Id, Name = o.Name, CustomerId = o.CustomerId, Amount = o.Amount, Probability = o.Probability, Stage = o.Stage,
            Status = o.Status, ExpectedCloseDate = o.ExpectedCloseDate, OwnerId = o.OwnerId, CreatedAt = o.CreatedAt,
            LeadId = o.LeadId, Source = o.Source, Notes = o.Notes, ClosedAt = o.ClosedAt,
            CustomerName = _db.Customers.IgnoreQueryFilters().Where(c => c.Id == o.CustomerId).Select(c => c.Name).FirstOrDefault() ?? ""
        }).FirstOrDefaultAsync() ?? throw new NotFoundException("Opportunity");

        await _owners.FillNamesAsync(new[] { view }, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return view;
    }

    public async Task<int> CreateAsync(OpportunityDetails details, Stage stage, string? ownerId, int? leadId = null)
    {
        await CheckLinksAsync(details.CustomerId, leadId);
        var deal = Opportunity.Open(details, stage, await _owners.PickAsync(ownerId), Now.Date, leadId);
        _db.Opportunities.Add(deal);
        await _db.SaveChangesAsync();
        return deal.Id;
    }

    public async Task UpdateAsync(int id, OpportunityDetails details, Stage stage, string? ownerId)
    {
        var deal = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == id) ?? throw new NotFoundException("Opportunity");
        await CheckLinksAsync(details.CustomerId, null);
        deal.Edit(details, stage, Now.Date, Now);
        deal.AssignTo(await _owners.PickAsync(ownerId, deal.OwnerId));
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        _owners.EnsureCanDelete();
        var deal = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == id) ?? throw new NotFoundException("Opportunity");
        deal.Delete();
        await _db.SaveChangesAsync();
    }

    public async Task<PipelineSummary> PipelineAsync()
    {
        var deals = await _db.Opportunities.AsNoTracking()
            .Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, o.OwnerId }).ToListAsync();

        var stages = Enum.GetValues<Stage>()
            .Select(s => new StageTotal(s, deals.Count(d => d.Stage == s), deals.Where(d => d.Stage == s).Sum(d => d.Amount)))
            .ToList();

        var open = deals.Where(d => d.Status == OpportunityStatus.Open).ToList();
        var owners = open.GroupBy(d => d.OwnerId)
            .Select(g => new OwnerTotal(g.Key, "", g.Count(), g.Sum(d => d.Amount), decimal.Round(g.Sum(d => d.Amount * d.Probability / 100m), 2)))
            .ToList();
        var named = new List<OwnerTotal>();
        await _owners.FillNamesAsync(owners, o => o.OwnerId, (o, n) => named.Add(o with { Owner = n }));

        return new PipelineSummary(open.Sum(d => d.Amount), decimal.Round(open.Sum(d => d.Amount * d.Probability / 100m), 2),
            stages, named.OrderByDescending(o => o.Amount).ToList());
    }

    public Task<List<Option>> OptionsAsync() =>
        _db.Opportunities.AsNoTracking().Where(o => o.Status == OpportunityStatus.Open).OrderBy(o => o.Name)
            .Select(o => new Option(o.Id, o.Name)).ToListAsync();

    private IQueryable<OpportunityRow> Rows(IQueryable<Opportunity> q) => q.Select(o => new OpportunityRow
    {
        Id = o.Id, Name = o.Name, CustomerId = o.CustomerId, Amount = o.Amount, Probability = o.Probability, Stage = o.Stage,
        Status = o.Status, ExpectedCloseDate = o.ExpectedCloseDate, OwnerId = o.OwnerId, CreatedAt = o.CreatedAt,
        CustomerName = _db.Customers.IgnoreQueryFilters().Where(c => c.Id == o.CustomerId).Select(c => c.Name).FirstOrDefault() ?? ""
    });

    // the customer (and lead) have to be records this user can actually see
    private async Task CheckLinksAsync(int customerId, int? leadId)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId))
            throw new DomainException("Pick one of your customers.", "CustomerId");
        if (leadId != null && !await _db.Leads.AnyAsync(l => l.Id == leadId))
            throw new DomainException("Pick one of your leads.", "LeadId");
    }
}
