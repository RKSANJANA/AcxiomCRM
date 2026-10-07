using Acxiom.Application;
using Acxiom.Application.Opportunities;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.Opportunities;

public class BoardModel : CrmPage
{
    private readonly OpportunityHandler _deals;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public BoardModel(OpportunityHandler deals, IUserDirectory users, ICurrentUser me)
    {
        _deals = deals;
        _users = users;
        _me = me;
    }

    public List<OpportunityRow> Deals { get; private set; } = new();
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();
    public string? Owner { get; private set; }

    public async Task OnGetAsync(string? owner)
    {
        Owner = owner;
        Deals = await _deals.BoardAsync(owner);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
