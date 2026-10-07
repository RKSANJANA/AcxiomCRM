using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Users;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Admin;

public class TeamsModel : CrmPage
{
    private readonly IUserAdmin _admin;

    public TeamsModel(IUserAdmin admin) => _admin = admin;

    public class Form
    {
        public int? Id { get; set; }
        [Required(ErrorMessage = "Give the team a name."), StringLength(80)] public string? Name { get; set; }
        [Display(Name = "Manager")] public string? ManagerId { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    public List<TeamSummary> Teams { get; private set; } = new();
    public List<UserOption> Managers { get; private set; } = new();
    public List<UserSummary> Users { get; private set; } = new();

    public async Task OnGetAsync(int? edit)
    {
        await LoadAsync();
        var t = Teams.FirstOrDefault(x => x.Id == edit);
        if (t != null) Input = new Form { Id = t.Id, Name = t.Name, ManagerId = t.ManagerId };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await TryAsync(() => _admin.SaveTeamAsync(Input.Id, Input.Name!, Input.ManagerId)))
        {
            await LoadAsync();
            return Page();
        }
        Toast("Team saved. Managers see the change within a minute.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _admin.DeleteTeamAsync(id);
        Toast("Team removed.");
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Teams = await _admin.TeamsAsync();
        Managers = await _admin.ManagersAsync();
        Users = await _admin.ListAsync(null, null, "active");
    }
}
