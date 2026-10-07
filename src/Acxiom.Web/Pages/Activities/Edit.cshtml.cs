using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Activities;
using Acxiom.Application.Customers;
using Acxiom.Application.Leads;
using Acxiom.Domain.Activities;
using Acxiom.Domain.Common;
using Acxiom.Domain.FollowUps;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Activities;

public class EditModel : CrmPage
{
    private readonly ActivityHandler _activities;
    private readonly CustomerHandler _customers;
    private readonly LeadHandler _leads;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public EditModel(ActivityHandler activities, CustomerHandler customers, LeadHandler leads, IUserDirectory users, ICurrentUser me)
    {
        _activities = activities;
        _customers = customers;
        _leads = leads;
        _users = users;
        _me = me;
    }

    public class Form
    {
        [Required] public ContactType Type { get; set; }
        [Required(ErrorMessage = "Subject is required."), StringLength(150)] public string? Subject { get; set; }
        [StringLength(Rules.NotesMax)] public string? Description { get; set; }
        [Required(ErrorMessage = "When did it happen?"), Display(Name = "Date & time")] public DateTime? When { get; set; }
        [Display(Name = "Customer")] public int? CustomerId { get; set; }
        [Display(Name = "Lead")] public int? LeadId { get; set; }
        [Required] public ActivityStatus Status { get; set; } = ActivityStatus.Done;
        [Display(Name = "Assigned to")] public string? OwnerId { get; set; }

        public ActivityDetails ToDetails() => new(Type, Subject, Description, When ?? DateTime.Now, CustomerId, LeadId, Status);
    }

    [BindProperty] public Form Input { get; set; } = new();
    public int? Id { get; private set; }
    public List<Option> Customers { get; private set; } = new();
    public List<Option> Leads { get; private set; } = new();
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(int? id, ContactType? type, int? customerId, int? leadId)
    {
        Id = id;
        if (id != null)
        {
            var a = await _activities.GetAsync(id.Value);
            Input = new Form
            {
                Type = a.Type, Subject = a.Subject, Description = a.Description, When = a.When, CustomerId = a.CustomerId,
                LeadId = a.LeadId, Status = a.Status, OwnerId = a.OwnerId
            };
        }
        else
        {
            Input = new Form { Type = type ?? ContactType.Call, When = DateTime.Now.AddMinutes(-DateTime.Now.Minute), CustomerId = customerId, LeadId = leadId, OwnerId = _me.Id };
        }
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        var ok = await TryAsync(async () =>
        {
            if (id == null) await _activities.CreateAsync(Input.ToDetails(), Input.OwnerId);
            else await _activities.UpdateAsync(id.Value, Input.ToDetails(), Input.OwnerId);
        });
        if (!ok)
        {
            await LoadListsAsync();
            return Page();
        }
        Toast(id == null ? $"{Input.Type} logged." : "Activity updated.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _activities.DeleteAsync(id);
        Toast("Activity deleted.");
        return RedirectToPage("Index");
    }

    private async Task LoadListsAsync()
    {
        Customers = await _customers.OptionsAsync();
        Leads = await _leads.OptionsAsync(openOnly: false);
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
