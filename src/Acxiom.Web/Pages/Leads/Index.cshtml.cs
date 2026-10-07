using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.Leads;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.Leads;

public class IndexModel : CrmPage
{
    private readonly LeadHandler _leads;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public IndexModel(LeadHandler leads, IUserDirectory users, ICurrentUser me)
    {
        _leads = leads;
        _users = users;
        _me = me;
    }

    public LeadFilter Filter { get; private set; } = new();
    public Paged<LeadRow> Result { get; private set; } = null!;
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(LeadFilter filter)
    {
        Filter = filter;
        Result = await _leads.SearchAsync(filter);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
