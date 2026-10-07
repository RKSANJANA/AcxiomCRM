using Acxiom.Application;
using Acxiom.Application.Activities;
using Acxiom.Application.Auditing;
using Acxiom.Application.Customers;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Opportunities;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.FollowUps;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Customers;

public class DetailsModel : CrmPage
{
    private readonly CustomerHandler _customers;
    private readonly OpportunityHandler _deals;
    private readonly FollowUpHandler _followUps;
    private readonly ActivityHandler _activities;
    private readonly AuditQuery _audit;

    public DetailsModel(CustomerHandler customers, OpportunityHandler deals, FollowUpHandler followUps, ActivityHandler activities, AuditQuery audit, ICurrentUser me)
    {
        _customers = customers;
        _deals = deals;
        _followUps = followUps;
        _activities = activities;
        _audit = audit;
        Me = me;
    }

    public ICurrentUser Me { get; }
    public CustomerView Customer { get; private set; } = null!;
    public IReadOnlyList<OpportunityRow> Deals { get; private set; } = Array.Empty<OpportunityRow>();
    public IReadOnlyList<FollowUpRow> FollowUps { get; private set; } = Array.Empty<FollowUpRow>();
    public IReadOnlyList<ActivityRow> Activities { get; private set; } = Array.Empty<ActivityRow>();
    public List<AuditEntry> History { get; private set; } = new();

    public async Task OnGetAsync(int id)
    {
        Customer = await _customers.GetAsync(id);
        Deals = (await _deals.SearchAsync(new OpportunityFilter { CustomerId = id, Size = 50 })).Items;
        FollowUps = (await _followUps.SearchAsync(new FollowUpFilter { Target = TargetKind.Customer, TargetId = id, Size = 50 })).Items;
        Activities = (await _activities.SearchAsync(new ActivityFilter { CustomerId = id, Size = 10 })).Items;
        History = await _audit.HistoryAsync("Customer", id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!await TryAsync(() => _customers.DeleteAsync(id), ""))
        {
            Toast(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            return RedirectToPage(new { id });
        }
        Toast("Customer deleted.");
        return RedirectToPage("Index");
    }
}
