using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Leads;
using Acxiom.Domain.Common;
using Acxiom.Domain.Leads;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Leads;

public class EditModel : CrmPage
{
    private readonly LeadHandler _leads;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public EditModel(LeadHandler leads, IUserDirectory users, ICurrentUser me)
    {
        _leads = leads;
        _users = users;
        _me = me;
    }

    public class Form
    {
        [Required(ErrorMessage = "Lead name is required."), StringLength(Rules.NameMax), Display(Name = "Lead name")]
        public string? Name { get; set; }

        [StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string? Email { get; set; }

        [RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.Messages.Phone), Display(Name = "Mobile number")]
        public string? Phone { get; set; }

        [StringLength(Rules.CompanyMax)] public string? Company { get; set; }

        [Required] public LeadSource Source { get; set; }
        [Required] public LeadStatus Status { get; set; } = LeadStatus.New;
        [Required] public LeadPriority Priority { get; set; } = LeadPriority.Normal;

        [Required(ErrorMessage = "Expected value is required."), Range(typeof(decimal), "0", "1000000000", ErrorMessage = Rules.Messages.ExpectedValue)]
        [RegularExpression(Rules.MoneyPattern, ErrorMessage = Rules.Messages.Money)]
        [Display(Name = "Expected value (₹)")]
        public decimal? ExpectedValue { get; set; }

        [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
        [Display(Name = "Assigned to")] public string? OwnerId { get; set; }

        public LeadDetails ToDetails() => new(Name, Email, Phone, Company, Source, Priority, ExpectedValue ?? 0, Notes);
    }

    [BindProperty] public Form Input { get; set; } = new();
    public int? Id { get; private set; }
    public IReadOnlyList<LeadStatus> StatusChoices { get; private set; } = new[] { LeadStatus.New, LeadStatus.Contacted };
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        if (id != null)
        {
            var l = await _leads.GetAsync(id.Value);
            if (l.IsClosed)
            {
                Toast($"This lead is {l.Status} and can't be edited.");
                return RedirectToPage("Details", new { id });
            }
            Input = new Form
            {
                Name = l.Name, Email = l.Email, Phone = l.Phone, Company = l.Company, Source = l.Source, Status = l.Status,
                Priority = l.Priority, ExpectedValue = l.ExpectedValue, Notes = l.Notes, OwnerId = l.OwnerId
            };
        }
        else Input.OwnerId = _me.Id;

        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        var newId = id;
        var ok = await TryAsync(async () =>
        {
            if (id == null) newId = await _leads.CreateAsync(Input.ToDetails(), Input.Status, Input.OwnerId);
            else await _leads.UpdateAsync(id.Value, Input.ToDetails(), Input.Status, Input.OwnerId);
        });
        if (!ok)
        {
            await LoadListsAsync();
            return Page();
        }
        Toast(id == null ? "Lead created." : "Lead updated.");
        return RedirectToPage("Details", new { id = newId });
    }

    private async Task LoadListsAsync()
    {
        if (Id != null)
        {
            var current = await _leads.GetAsync(Id.Value);
            StatusChoices = new[] { current.Status }.Concat(current.NextStatuses).ToList();
        }
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
