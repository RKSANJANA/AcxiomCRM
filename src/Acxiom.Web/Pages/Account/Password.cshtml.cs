using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Infrastructure.Identity;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Account;

public class PasswordModel : CrmPage
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly IAuditLog _audit;

    public PasswordModel(SignInManager<AppUser> signIn, IAuditLog audit)
    {
        _signIn = signIn;
        _audit = audit;
    }

    public class Form
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string Current { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password), Display(Name = "New password")]
        public string New { get; set; } = "";

        [Required, Compare(nameof(New), ErrorMessage = "The passwords don't match."), DataType(DataType.Password), Display(Name = "Repeat new password")]
        public string Confirm { get; set; } = "";
    }

    [BindProperty] public Form Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _signIn.UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _signIn.UserManager.ChangePasswordAsync(user, Input.Current, Input.New);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code == "PasswordMismatch" ? "Input.Current" : "Input.New",
                    e.Code == "PasswordMismatch" ? "That's not your current password." : e.Description);
            return Page();
        }

        await _signIn.RefreshSignInAsync(user);
        await _audit.WriteAsync("PasswordChange", "Account", user.Id);
        Toast("Password changed.");
        return RedirectToPage("/Index");
    }
}
