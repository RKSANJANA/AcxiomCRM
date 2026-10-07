using Acxiom.Application;
using Acxiom.Application.Dashboard;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Users;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages;

public class IndexModel : CrmPage
{
    private readonly DashboardHandler _dashboard;
    private readonly FollowUpHandler _followUps;
    private readonly IUserAdmin _admin;
    private readonly ICurrentUser _me;

    public IndexModel(DashboardHandler dashboard, FollowUpHandler followUps, IUserAdmin admin, ICurrentUser me)
    {
        _dashboard = dashboard;
        _followUps = followUps;
        _admin = admin;
        _me = me;
    }

    [BindProperty(SupportsGet = true)] public Period Period { get; set; } = Period.All;
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    public DashboardData Data { get; private set; } = null!;
    public IReadOnlyList<FollowUpRow> NextUp { get; private set; } = Array.Empty<FollowUpRow>();
    public SecurityStats? Security { get; private set; }
    public string Scope => _me.IsAdmin ? "Everyone" : _me.IsManager ? "You and your team" : "Your records";
    public bool ShowTeam => _me.IsAdmin || _me.IsManager;

    public async Task OnGetAsync()
    {
        Data = await _dashboard.LoadAsync(Period, From, To);
        NextUp = (await _followUps.SearchAsync(new FollowUpFilter { Due = "pending", Size = 6 })).Items;
        if (_me.IsAdmin) Security = await _admin.StatsAsync();
    }
}
