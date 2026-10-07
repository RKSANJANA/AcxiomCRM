using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Customers;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Leads;
using Acxiom.Application.Opportunities;
using Acxiom.Domain.Common;
using Acxiom.Domain.FollowUps;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Acxiom.Web.Pages.FollowUps;

public class EditModel : CrmPage
{
    private readonly FollowUpHandler _followUps;
    private readonly CustomerHandler _customers;
    private readonly LeadHandler _leads;
    private readonly OpportunityHandler _deals;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public EditModel(FollowUpHandler followUps, CustomerHandler customers, LeadHandler leads, OpportunityHandler deals, IUserDirectory users, ICurrentUser me)
    {
        _followUps = followUps;
        _customers = customers;
        _leads = leads;
        _deals = deals;
        _users = users;
        _me = me;
    }

    public class Form
    {
        // "Customer-3", "Lead-8", "Opportunity-2"
        [Display(Name = "For")] public string? Target { get; set; }

        [Required(ErrorMessage = "Pick a date and time."), Display(Name = "Date & time")]
        [NotBeforeToday(ErrorMessage = Rules.Messages.FollowUpDate)]
        public DateTime? DueAt { get; set; }

        [Required(ErrorMessage = "Subject is required."), StringLength(150)]
        public string? Subject { get; set; }

        [Required] public ContactType Type { get; set; } = ContactType.Call;
        [StringLength(Rules.NotesMax)] public string? Remarks { get; set; }
        [Display(Name = "Assigned to")] public string? OwnerId { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    public int? Id { get; private set; }
    public string? TargetLabel { get; private set; }
    public List<SelectListItem> Targets { get; private set; } = new();
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task<IActionResult> OnGetAsync(int? id, TargetKind? target, int? targetId)
    {
        Id = id;
        if (id != null)
        {
            var f = await _followUps.GetAsync(id.Value);
            if (f.Status != FollowUpStatus.Planned)
            {
                Toast("Only planned follow-ups can be edited.");
                return RedirectToPage("Details", new { id });
            }
            Input = new Form { DueAt = f.DueAt, Subject = f.Subject, Type = f.Type, Remarks = f.Remarks, OwnerId = f.OwnerId };
            TargetLabel = $"{f.Target}: {f.TargetName}";
        }
        else
        {
            Input = new Form { Target = target != null ? $"{target}-{targetId}" : null, DueAt = DateTime.Today.AddDays(1).AddHours(11), OwnerId = _me.Id };
        }
        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        var newId = id;
        var ok = await TryAsync(async () =>
        {
            if (id != null)
            {
                await _followUps.UpdateAsync(id.Value, Input.DueAt ?? default, Input.Subject, Input.Type, Input.Remarks, Input.OwnerId);
                return;
            }
            var parts = Input.Target?.Split('-');
            if (parts is not { Length: 2 } || !Enum.TryParse<TargetKind>(parts[0], out var kind) || !int.TryParse(parts[1], out var targetId))
                throw new DomainException("Pick what this follow-up is for.", "Target");
            newId = await _followUps.ScheduleAsync(kind, targetId, Input.DueAt ?? default, Input.Subject, Input.Type, Input.Remarks, Input.OwnerId);
        });
        if (!ok)
        {
            if (id != null) TargetLabel = (await _followUps.GetAsync(id.Value)) is var f ? $"{f.Target}: {f.TargetName}" : null;
            await LoadListsAsync();
            return Page();
        }
        Toast(id == null ? "Follow-up scheduled." : "Follow-up updated.");
        return RedirectToPage("Details", new { id = newId });
    }

    private async Task LoadListsAsync()
    {
        if (Id == null)
        {
            SelectListItem Item(SelectListGroup g, TargetKind kind, Option o) =>
                new(o.Text, $"{kind}-{o.Id}", Input.Target == $"{kind}-{o.Id}") { Group = g };

            var cg = new SelectListGroup { Name = "Customers" };
            var lg = new SelectListGroup { Name = "Leads" };
            var og = new SelectListGroup { Name = "Opportunities" };
            Targets = (await _customers.OptionsAsync()).Select(o => Item(cg, TargetKind.Customer, o))
                .Concat((await _leads.OptionsAsync(openOnly: true)).Select(o => Item(lg, TargetKind.Lead, o)))
                .Concat((await _deals.OptionsAsync()).Select(o => Item(og, TargetKind.Opportunity, o)))
                .ToList();
        }
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
