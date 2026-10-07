using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.Opportunities;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.Opportunities;

public class IndexModel : CrmPage
{
    private readonly OpportunityHandler _deals;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public IndexModel(OpportunityHandler deals, IUserDirectory users, ICurrentUser me)
    {
        _deals = deals;
        _users = users;
        _me = me;
    }

    public OpportunityFilter Filter { get; private set; } = new();
    public Paged<OpportunityRow> Result { get; private set; } = null!;
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(OpportunityFilter filter)
    {
        Filter = filter;
        Result = await _deals.SearchAsync(filter);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
