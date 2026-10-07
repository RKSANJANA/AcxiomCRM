using Acxiom.Application;
using Acxiom.Application.Activities;
using Acxiom.Application.Auditing;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Leads;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.FollowUps;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Leads;

public class DetailsModel : CrmPage
{
    private readonly LeadHandler _leads;
    private readonly FollowUpHandler _followUps;
    private readonly ActivityHandler _activities;
    private readonly AuditQuery _audit;

    public DetailsModel(LeadHandler leads, FollowUpHandler followUps, ActivityHandler activities, AuditQuery audit, ICurrentUser me)
    {
        _leads = leads;
        _followUps = followUps;
        _activities = activities;
        _audit = audit;
        Me = me;
    }

    public ICurrentUser Me { get; }
    public LeadView Lead { get; private set; } = null!;
    public IReadOnlyList<FollowUpRow> FollowUps { get; private set; } = Array.Empty<FollowUpRow>();
    public IReadOnlyList<ActivityRow> Activities { get; private set; } = Array.Empty<ActivityRow>();
    public List<AuditEntry> History { get; private set; } = new();

    public async Task OnGetAsync(int id)
    {
        Lead = await _leads.GetAsync(id);
        FollowUps = (await _followUps.SearchAsync(new FollowUpFilter { Target = TargetKind.Lead, TargetId = id, Size = 50 })).Items;
        Activities = (await _activities.SearchAsync(new ActivityFilter { LeadId = id, Size = 10 })).Items;
        History = await _audit.HistoryAsync("Lead", id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _leads.DeleteAsync(id);
        Toast("Lead deleted.");
        return RedirectToPage("Index");
    }
}
