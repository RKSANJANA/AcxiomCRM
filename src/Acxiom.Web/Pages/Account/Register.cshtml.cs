using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Domain;
using Acxiom.Domain.Common;
using Acxiom.Infrastructure.Identity;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Acxiom.Web.Pages.Account;

[EnableRateLimiting("signin")]
public class RegisterModel : CrmPage
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly IAuditLog _audit;

    public RegisterModel(SignInManager<AppUser> signIn, IAuditLog audit)
    {
        _signIn = signIn;
        _audit = audit;
    }

    public class Form
    {
        [Required, StringLength(100, MinimumLength = 2), Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required, StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string Email { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required, Compare(nameof(Password), ErrorMessage = "The passwords don't match."), DataType(DataType.Password), Display(Name = "Repeat password")]
        public string Confirm { get; set; } = "";
    }

    [BindProperty] public Form Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();
        var user = new AppUser { UserName = email, Email = email, FullName = Input.FullName.Trim(), LockoutEnabled = true };
        var result = await _signIn.UserManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code.StartsWith("Password") ? "Input.Password" : e.Code.Contains("Email") || e.Code.Contains("UserName") ? "Input.Email" : "",
                    e.Code.StartsWith("Duplicate") ? "That email is already registered." : e.Description);
            return Page();
        }

        // everyone starts as a sales executive; an admin can change it later
        await _signIn.UserManager.AddToRoleAsync(user, Roles.SalesExecutive);
        await _audit.WriteAsync("Register", "Account", user.Id, "self registration", userId: user.Id, userName: email);
        await _signIn.SignInAsync(user, false);
        Toast("Welcome aboard!");
        return RedirectToPage("/Index");
    }
}
