using Acxiom.Application;
using Acxiom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acxiom.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly IAuditLog _audit;
    private readonly ICurrentUser _me;

    public LogoutModel(SignInManager<AppUser> signIn, IAuditLog audit, ICurrentUser me)
    {
        _signIn = signIn;
        _audit = audit;
        _me = me;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await _audit.WriteAsync("Logout", "Account", _me.Id);
        await _signIn.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
