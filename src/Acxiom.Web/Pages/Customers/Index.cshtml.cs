using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.Customers;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Customers;

public class IndexModel : CrmPage
{
    private readonly CustomerHandler _customers;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public IndexModel(CustomerHandler customers, IUserDirectory users, ICurrentUser me)
    {
        _customers = customers;
        _users = users;
        _me = me;
    }

    public CustomerFilter Filter { get; private set; } = new();
    public Paged<CustomerRow> Result { get; private set; } = null!;
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(CustomerFilter filter)
    {
        Filter = filter;
        Result = await _customers.SearchAsync(filter);
        if (!_me.SeesEverything && _me.OwnerIds.Count <= 1) return;
        Owners = await _users.AssignableAsync();
    }
}
