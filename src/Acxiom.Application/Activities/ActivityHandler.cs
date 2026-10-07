using Acxiom.Application.Common;
using Acxiom.Domain.Activities;
using Acxiom.Domain.Common;
using Acxiom.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Activities;

public class ActivityFilter : ListFilter
{
    public ContactType? Type { get; set; }
    public ActivityStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
}

public class ActivityRow
{
    public int Id { get; set; }
    public ContactType Type { get; set; }
    public string Subject { get; set; } = "";
    public string? Description { get; set; }
    public DateTime When { get; set; }
    public ActivityStatus Status { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
}

public class ActivityHandler
{
    private readonly IAppDbContext _db;
    private readonly Owners _owners;

    public ActivityHandler(IAppDbContext db, Owners owners)
    {
        _db = db;
        _owners = owners;
    }

    public async Task<Paged<ActivityRow>> SearchAsync(ActivityFilter f)
    {
        var text = f.Text;
        var q = _db.Activities.AsNoTracking()
            .WhereIf(text != null, a => a.Subject.ToLower().Contains(text!) || (a.Description != null && a.Description.ToLower().Contains(text!)))
            .WhereIf(f.Type != null, a => a.Type == f.Type)
            .WhereIf(f.Status != null, a => a.Status == f.Status)
            .WhereIf(f.From != null, a => a.When >= f.From!.Value.Date)
            .WhereIf(f.To != null, a => a.When < f.To!.Value.Date.AddDays(1))
            .WhereIf(f.CustomerId != null, a => a.CustomerId == f.CustomerId)
            .WhereIf(f.LeadId != null, a => a.LeadId == f.LeadId)
            .WhereIf(!string.IsNullOrEmpty(f.Owner), a => a.OwnerId == f.Owner);

        q = f.Sort switch
        {
            "when" => q.OrderBy(a => a.When),
            "type" => q.OrderBy(a => a.Type).ThenByDescending(a => a.When),
            "subject" => q.OrderBy(a => a.Subject),
            _ => q.OrderByDescending(a => a.When)
        };

        var page = await Rows(q).PageAsync(f);
        await _owners.FillNamesAsync(page.Items, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return page;
    }

    public async Task<ActivityRow> GetAsync(int id)
    {
        var row = await Rows(_db.Activities.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync()
                  ?? throw new NotFoundException("Activity");
        await _owners.FillNamesAsync(new[] { row }, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return row;
    }

    public async Task<int> CreateAsync(ActivityDetails details, string? ownerId)
    {
        await CheckLinksAsync(details);
        var activity = Activity.Log(details, await _owners.PickAsync(ownerId));
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        return activity.Id;
    }

    public async Task UpdateAsync(int id, ActivityDetails details, string? ownerId)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NotFoundException("Activity");
        await CheckLinksAsync(details);
        activity.Update(details);
        activity.AssignTo(await _owners.PickAsync(ownerId, activity.OwnerId));
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NotFoundException("Activity");
        activity.Delete();
        await _db.SaveChangesAsync();
    }

    private async Task CheckLinksAsync(ActivityDetails d)
    {
        if (d.CustomerId != null && !await _db.Customers.AnyAsync(c => c.Id == d.CustomerId))
            throw new DomainException("Pick one of your customers.", "CustomerId");
        if (d.LeadId != null && !await _db.Leads.AnyAsync(l => l.Id == d.LeadId))
            throw new DomainException("Pick one of your leads.", "LeadId");
    }

    private IQueryable<ActivityRow> Rows(IQueryable<Activity> q) => q.Select(a => new ActivityRow
    {
        Id = a.Id, Type = a.Type, Subject = a.Subject, Description = a.Description, When = a.When, Status = a.Status,
        CustomerId = a.CustomerId, LeadId = a.LeadId, OwnerId = a.OwnerId,
        CustomerName = _db.Customers.IgnoreQueryFilters().Where(c => c.Id == a.CustomerId).Select(c => c.Name).FirstOrDefault(),
        LeadName = _db.Leads.IgnoreQueryFilters().Where(l => l.Id == a.LeadId).Select(l => l.Name).FirstOrDefault()
    });
}
