using Acxiom.Application.Users;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Admin.Users;

public class IndexModel : CrmPage
{
    private readonly IUserAdmin _admin;

    public IndexModel(IUserAdmin admin) => _admin = admin;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public string? State { get; set; }
    public List<UserSummary> Users { get; private set; } = new();

    public async Task OnGetAsync() => Users = await _admin.ListAsync(Q, Role, State);
}
