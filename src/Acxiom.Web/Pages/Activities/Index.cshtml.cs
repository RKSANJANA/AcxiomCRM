using Acxiom.Application;
using Acxiom.Application.Activities;
using Acxiom.Application.Common;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.Activities;

public class IndexModel : CrmPage
{
    private readonly ActivityHandler _activities;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public IndexModel(ActivityHandler activities, IUserDirectory users, ICurrentUser me)
    {
        _activities = activities;
        _users = users;
        _me = me;
    }

    public ActivityFilter Filter { get; private set; } = new();
    public Paged<ActivityRow> Result { get; private set; } = null!;
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(ActivityFilter filter)
    {
        Filter = filter;
        Result = await _activities.SearchAsync(filter);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
