using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.FollowUps;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.FollowUps;

public class IndexModel : CrmPage
{
    private readonly FollowUpHandler _followUps;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public IndexModel(FollowUpHandler followUps, IUserDirectory users, ICurrentUser me)
    {
        _followUps = followUps;
        _users = users;
        _me = me;
    }

    public FollowUpFilter Filter { get; private set; } = new();
    public Paged<FollowUpRow> Result { get; private set; } = null!;
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(FollowUpFilter filter)
    {
        Filter = filter;
        Result = await _followUps.SearchAsync(filter);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
