using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Domain.Common;
using Acxiom.Infrastructure.Identity;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Acxiom.Web.Pages.Account;

[EnableRateLimiting("signin")]
public class LoginModel : CrmPage
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly IAuditLog _audit;

    public LoginModel(SignInManager<AppUser> signIn, IAuditLog audit)
    {
        _signIn = signIn;
        _audit = audit;
    }

    public class Form
    {
        [Required, RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Keep me signed in")]
        public bool Remember { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? LockedMessage { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();
        var user = await _signIn.UserManager.FindByEmailAsync(email);
        if (user == null)
        {
            await _audit.WriteAsync("LoginFailed", "Account", note: "unknown email", succeeded: false, userName: email);
            return Fail();
        }

        // password first (this is what counts towards lockout), then the active flag
        var result = await _signIn.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await _audit.WriteAsync("Lockout", "Account", user.Id, "too many failed attempts", false, user.Id, email);
            LockedMessage = "This account is locked for a while after too many failed attempts. Try again later or ask an admin to unlock it.";
            return Page();
        }
        if (!result.Succeeded)
        {
            await _audit.WriteAsync("LoginFailed", "Account", user.Id, "wrong password", false, user.Id, email);
            return Fail();
        }
        if (!user.IsActive)
        {
            await _audit.WriteAsync("LoginFailed", "Account", user.Id, "account disabled", false, user.Id, email);
            ModelState.AddModelError("", "This account has been disabled.");
            return Page();
        }

        await _signIn.SignInAsync(user, Input.Remember);
        await _audit.WriteAsync("Login", "Account", user.Id, userId: user.Id, userName: email);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    private IActionResult Fail()
    {
        ModelState.AddModelError("", "Email or password is incorrect.");
        return Page();
    }
}
