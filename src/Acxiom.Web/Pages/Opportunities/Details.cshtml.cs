using Acxiom.Application;
using Acxiom.Application.Auditing;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Opportunities;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.FollowUps;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Opportunities;

public class DetailsModel : CrmPage
{
    private readonly OpportunityHandler _deals;
    private readonly FollowUpHandler _followUps;
    private readonly AuditQuery _audit;

    public DetailsModel(OpportunityHandler deals, FollowUpHandler followUps, AuditQuery audit, ICurrentUser me)
    {
        _deals = deals;
        _followUps = followUps;
        _audit = audit;
        Me = me;
    }

    public ICurrentUser Me { get; }
    public OpportunityView Deal { get; private set; } = null!;
    public IReadOnlyList<FollowUpRow> FollowUps { get; private set; } = Array.Empty<FollowUpRow>();
    public List<AuditEntry> History { get; private set; } = new();

    public async Task OnGetAsync(int id)
    {
        Deal = await _deals.GetAsync(id);
        FollowUps = (await _followUps.SearchAsync(new FollowUpFilter { Target = TargetKind.Opportunity, TargetId = id, Size = 50 })).Items;
        History = await _audit.HistoryAsync("Opportunity", id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _deals.DeleteAsync(id);
        Toast("Opportunity deleted.");
        return RedirectToPage("Index");
    }
}
