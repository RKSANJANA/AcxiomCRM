using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.Users;
using Acxiom.Domain;
using Acxiom.Domain.Common;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Admin.Users;

public class EditModel : CrmPage
{
    private readonly IUserAdmin _admin;
    private readonly ICurrentUser _me;

    public EditModel(IUserAdmin admin, ICurrentUser me)
    {
        _admin = admin;
        _me = me;
    }

    public class Form
    {
        [Required, StringLength(100, MinimumLength = 2), Display(Name = "Full name")]
        public string? FullName { get; set; }

        [Required, StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string? Email { get; set; }

        [Required] public string Role { get; set; } = Roles.SalesExecutive;
        [Display(Name = "Team")] public int? TeamId { get; set; }
        [Display(Name = "Account enabled")] public bool IsActive { get; set; } = true;

        [DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [Display(Name = "Initial password")]
        public string? Password { get; set; }
    }

    public class PasswordForm
    {
        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), Display(Name = "New password")]
        public string? NewPassword { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    public PasswordForm Reset { get; set; } = new();
    public string? Id { get; private set; }
    public UserSummary? Current { get; private set; }
    public List<TeamSummary> Teams { get; private set; } = new();
    public bool IsMe => Id != null && Id == _me.Id;

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        Id = id;
        if (id != null)
        {
            Current = await _admin.FindAsync(id) ?? throw new NotFoundException("User");
            Input = new Form { FullName = Current.FullName, Email = Current.Email, Role = Current.Role, TeamId = Current.TeamId, IsActive = Current.IsActive };
        }
        Teams = await _admin.TeamsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id)
    {
        Id = id;
        if (id == null && string.IsNullOrEmpty(Input.Password))
            ModelState.AddModelError("Input.Password", "Set a starting password for the new user.");

        var save = new SaveUser(Input.FullName ?? "", Input.Email ?? "", Input.Role, Input.TeamId, Input.IsActive, Input.Password);
        var ok = await TryAsync(async () =>
        {
            if (id == null) await _admin.CreateAsync(save);
            else await _admin.UpdateAsync(id, save);
        });
        if (!ok) return await RedisplayAsync(id);

        Toast(id == null ? "User created." : "User saved.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetAsync(string id, [Bind(Prefix = "Reset")] PasswordForm reset)
    {
        Id = id;
        Reset = reset;
        // only the reset form was posted; the main form's fields aren't part of this request
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Input")).ToList()) ModelState.Remove(key);
        if (!await TryAsync(() => _admin.ResetPasswordAsync(id, reset.NewPassword!), "Reset")) return await RedisplayAsync(id);
        Toast("Password reset - the user's lockout was cleared too.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnlockAsync(string id)
    {
        await _admin.UnlockAsync(id);
        Toast("Account unlocked.");
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> RedisplayAsync(string? id)
    {
        if (id != null) Current = await _admin.FindAsync(id);
        Teams = await _admin.TeamsAsync();
        return Page();
    }
}
