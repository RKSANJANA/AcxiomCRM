using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Customers;
using Acxiom.Application.Opportunities;
using Acxiom.Domain.Common;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Opportunities;

public class EditModel : CrmPage
{
    private readonly OpportunityHandler _deals;
    private readonly CustomerHandler _customers;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public EditModel(OpportunityHandler deals, CustomerHandler customers, IUserDirectory users, ICurrentUser me)
    {
        _deals = deals;
        _customers = customers;
        _users = users;
        _me = me;
    }

    public class Form
    {
        [Required(ErrorMessage = "Opportunity name is required."), StringLength(150), Display(Name = "Opportunity name")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Pick a customer."), Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Amount is required."), Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = Rules.Messages.Amount)]
        [RegularExpression(Rules.MoneyPattern, ErrorMessage = Rules.Messages.Money), Display(Name = "Amount (₹)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Probability is required."), Range(0, 100, ErrorMessage = Rules.Messages.Probability), Display(Name = "Probability %")]
        public int? Probability { get; set; }

        [Required] public Stage Stage { get; set; } = Stage.Qualification;

        // only enforced while the deal stays open
        [Required(ErrorMessage = "Expected close date is required."), DataType(DataType.Date), Display(Name = "Expected close date")]
        [NotBeforeToday(UnlessField = nameof(Stage), UnlessValues = "Won,Lost", ErrorMessage = Rules.Messages.CloseDate)]
        public DateTime? ExpectedCloseDate { get; set; }

        public LeadSource? Source { get; set; }
        [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
        [Display(Name = "Owner")] public string? OwnerId { get; set; }

        public OpportunityDetails ToDetails() =>
            new(Name, CustomerId ?? 0, Amount ?? 0, Probability ?? -1, ExpectedCloseDate ?? default, Source, Notes);
    }

    [BindProperty] public Form Input { get; set; } = new();
    public int? Id { get; private set; }
    public List<Option> Customers { get; private set; } = new();
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();
    public Stage[] StageChoices => Id == null ? new[] { Stage.Qualification, Stage.Proposal, Stage.Negotiation } : Enum.GetValues<Stage>();

    public async Task<IActionResult> OnGetAsync(int? id, int? customerId)
    {
        Id = id;
        if (id != null)
        {
            var o = await _deals.GetAsync(id.Value);
            if (o.Status != OpportunityStatus.Open)
            {
                Toast($"This opportunity is already {o.Status}.");
                return RedirectToPage("Details", new { id });
            }
            Input = new Form
            {
                Name = o.Name, CustomerId = o.CustomerId, Amount = o.Amount, Probability = o.Probability, Stage = o.Stage,
                ExpectedCloseDate = o.ExpectedCloseDate, Source = o.Source, Notes = o.Notes, OwnerId = o.OwnerId
            };
        }
        else
        {
            Input = new Form { CustomerId = customerId, Probability = 20, ExpectedCloseDate = DateTime.Today.AddMonths(1), OwnerId = _me.Id };
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
            if (id == null) newId = await _deals.CreateAsync(Input.ToDetails(), Input.Stage, Input.OwnerId);
            else await _deals.UpdateAsync(id.Value, Input.ToDetails(), Input.Stage, Input.OwnerId);
        });
        if (!ok)
        {
            await LoadListsAsync();
            return Page();
        }
        Toast(Input.Stage is Stage.Won or Stage.Lost ? $"Opportunity closed as {Input.Stage}." : "Opportunity saved.");
        return RedirectToPage("Details", new { id = newId });
    }

    private async Task LoadListsAsync()
    {
        Customers = await _customers.OptionsAsync();
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
