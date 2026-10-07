using System.Linq.Expressions;
using Acxiom.Application.Common;
using Acxiom.Domain.Activities;
using Acxiom.Domain.Common;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.FollowUps;

public class FollowUpFilter : ListFilter
{
    public FollowUpStatus? Status { get; set; }
    public ContactType? Type { get; set; }
    public TargetKind? Target { get; set; }
    public int? TargetId { get; set; }

    /// <summary>today | overdue | upcoming | pending</summary>
    public string? Due { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class FollowUpRow
{
    public int Id { get; set; }
    public DateTime DueAt { get; set; }
    public string Subject { get; set; } = "";
    public ContactType Type { get; set; }
    public FollowUpStatus Status { get; set; }
    public TargetKind Target { get; set; }
    public int TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public string? Remarks { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public bool Overdue { get; set; }
}

public class FollowUpHandler
{
    private static readonly Dictionary<string, Expression<Func<FollowUp, object?>>> SortColumns = new()
    {
        ["due"] = f => f.DueAt,
        ["subject"] = f => f.Subject,
        ["type"] = f => f.Type,
        ["status"] = f => f.Status,
    };

    private readonly IAppDbContext _db;
    private readonly Owners _owners;
    private readonly TimeProvider _clock;

    public FollowUpHandler(IAppDbContext db, Owners owners, TimeProvider clock)
    {
        _db = db;
        _owners = owners;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    public async Task<Paged<FollowUpRow>> SearchAsync(FollowUpFilter f)
    {
        var today = Now.Date;
        var tomorrow = today.AddDays(1);
        var text = f.Text;
        var planned = FollowUpStatus.Planned;

        var q = _db.FollowUps.AsNoTracking()
            .WhereIf(text != null, x => x.Subject.ToLower().Contains(text!))
            .WhereIf(f.Status != null, x => x.Status == f.Status)
            .WhereIf(f.Type != null, x => x.Type == f.Type)
            .WhereIf(f.From != null, x => x.DueAt >= f.From!.Value.Date)
            .WhereIf(f.To != null, x => x.DueAt < f.To!.Value.Date.AddDays(1))
            .WhereIf(!string.IsNullOrEmpty(f.Owner), x => x.OwnerId == f.Owner)
            .WhereIf(f.Target == TargetKind.Customer, x => x.CustomerId != null && (f.TargetId == null || x.CustomerId == f.TargetId))
            .WhereIf(f.Target == TargetKind.Lead, x => x.LeadId != null && (f.TargetId == null || x.LeadId == f.TargetId))
            .WhereIf(f.Target == TargetKind.Opportunity, x => x.OpportunityId != null && (f.TargetId == null || x.OpportunityId == f.TargetId));

        q = f.Due switch
        {
            "today" => q.Where(x => x.Status == planned && x.DueAt >= today && x.DueAt < tomorrow),
            "overdue" => q.Where(x => x.Status == planned && x.DueAt < today),
            "upcoming" => q.Where(x => x.Status == planned && x.DueAt >= today),
            "pending" => q.Where(x => x.Status == planned),
            _ => q
        };

        var fallback = f.Due is null or "" ? "-due" : "due";
        var page = await Rows(q.SortBy(f.Sort, SortColumns, fallback), today).PageAsync(f);
        await _owners.FillNamesAsync(page.Items, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return page;
    }

    public async Task<FollowUpRow> GetAsync(int id)
    {
        var row = await Rows(_db.FollowUps.AsNoTracking().Where(x => x.Id == id), Now.Date).FirstOrDefaultAsync()
                  ?? throw new NotFoundException("Follow-up");
        await _owners.FillNamesAsync(new[] { row }, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return row;
    }

    public async Task<int> ScheduleAsync(TargetKind target, int targetId, DateTime dueAt, string? subject, ContactType type,
        string? remarks, string? ownerId)
    {
        var visible = target switch
        {
            TargetKind.Customer => await _db.Customers.AnyAsync(c => c.Id == targetId),
            TargetKind.Lead => await _db.Leads.AnyAsync(l => l.Id == targetId),
            TargetKind.Opportunity => await _db.Opportunities.AnyAsync(o => o.Id == targetId),
            _ => false
        };
        if (!visible) throw new DomainException("Pick a customer, lead or opportunity you have access to.", "Target");

        var item = FollowUp.Schedule(target, targetId, dueAt, subject, type, remarks, await _owners.PickAsync(ownerId), Now.Date);
        _db.FollowUps.Add(item);
        await _db.SaveChangesAsync();
        return item.Id;
    }

    public async Task UpdateAsync(int id, DateTime dueAt, string? subject, ContactType type, string? remarks, string? ownerId)
    {
        var item = await Load(id);
        item.Change(dueAt, subject, type, remarks, Now.Date);
        item.AssignTo(await _owners.PickAsync(ownerId, item.OwnerId));
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Completing writes an activity to the history, and a lead that was still "New"
    /// moves to "Contacted" since we've now spoken to them.
    /// </summary>
    public async Task CompleteAsync(int id, string? outcome)
    {
        var item = await Load(id);
        item.Complete(outcome, Now);

        int? customerId = item.CustomerId;
        if (item.OpportunityId != null)
            customerId = await _db.Opportunities.IgnoreQueryFilters().Where(o => o.Id == item.OpportunityId).Select(o => (int?)o.CustomerId).FirstOrDefaultAsync();

        _db.Activities.Add(Activity.Log(new ActivityDetails(item.Type, item.Subject, item.Remarks ?? "Follow-up done", Now,
            customerId, item.LeadId, ActivityStatus.Done), item.OwnerId));

        if (item.LeadId != null)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == item.LeadId);
            if (lead?.Status == LeadStatus.New) lead.MoveTo(LeadStatus.Contacted);
        }

        await _db.SaveChangesAsync();
    }

    public async Task RescheduleAsync(int id, DateTime newDue)
    {
        var item = await Load(id);
        item.Reschedule(newDue, Now.Date);
        await _db.SaveChangesAsync();
    }

    public async Task MarkMissedAsync(int id)
    {
        var item = await Load(id);
        item.MarkMissed();
        await _db.SaveChangesAsync();
    }

    public async Task CancelAsync(int id)
    {
        var item = await Load(id);
        item.Cancel();
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        _owners.EnsureCanDelete();
        var item = await Load(id);
        item.Delete();
        await _db.SaveChangesAsync();
    }

    public Task<int> DueCountAsync()
    {
        var tomorrow = Now.Date.AddDays(1);
        return _db.FollowUps.CountAsync(x => x.Status == FollowUpStatus.Planned && x.DueAt < tomorrow);
    }

    private async Task<FollowUp> Load(int id) =>
        await _db.FollowUps.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Follow-up");

    private IQueryable<FollowUpRow> Rows(IQueryable<FollowUp> q, DateTime today) => q.Select(x => new FollowUpRow
    {
        Id = x.Id, DueAt = x.DueAt, Subject = x.Subject, Type = x.Type, Status = x.Status, Remarks = x.Remarks,
        CompletedAt = x.CompletedAt, OwnerId = x.OwnerId,
        Overdue = x.Status == FollowUpStatus.Planned && x.DueAt < today,
        Target = x.OpportunityId != null ? TargetKind.Opportunity : x.LeadId != null ? TargetKind.Lead : TargetKind.Customer,
        TargetId = x.OpportunityId ?? x.LeadId ?? x.CustomerId ?? 0,
        TargetName = x.OpportunityId != null
            ? _db.Opportunities.IgnoreQueryFilters().Where(o => o.Id == x.OpportunityId).Select(o => o.Name).FirstOrDefault() ?? ""
            : x.LeadId != null
                ? _db.Leads.IgnoreQueryFilters().Where(l => l.Id == x.LeadId).Select(l => l.Name).FirstOrDefault() ?? ""
                : _db.Customers.IgnoreQueryFilters().Where(c => c.Id == x.CustomerId).Select(c => c.Name).FirstOrDefault() ?? ""
    });
}
