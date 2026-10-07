using System.ComponentModel.DataAnnotations;
using Acxiom.Application.Leads;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Leads;

public class ConvertModel : CrmPage
{
    private readonly LeadHandler _leads;

    public ConvertModel(LeadHandler leads) => _leads = leads;

    public class Form
    {
        [Required(ErrorMessage = "Customer Name is required."), StringLength(Rules.NameMax), Display(Name = "Customer name")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Email is required."), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Phone is required."), RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.Messages.Phone), Display(Name = "Mobile number")]
        public string? Phone { get; set; }

        [StringLength(Rules.CompanyMax)] public string? Company { get; set; }

        [Required(ErrorMessage = "Give the opportunity a name."), StringLength(150), Display(Name = "Opportunity name")]
        public string? DealName { get; set; }

        [Required(ErrorMessage = "Amount is required."), Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = Rules.Messages.Amount)]
        [RegularExpression(Rules.MoneyPattern, ErrorMessage = Rules.Messages.Money), Display(Name = "Amount (₹)")]
        public decimal? Amount { get; set; }

        [Required, Range(0, 100, ErrorMessage = Rules.Messages.Probability), Display(Name = "Probability %")]
        public int? Probability { get; set; }

        [Required(ErrorMessage = "Expected close date is required."), DataType(DataType.Date)]
        [NotBeforeToday(ErrorMessage = Rules.Messages.CloseDate), Display(Name = "Expected close date")]
        public DateTime? CloseDate { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    public LeadView Lead { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Lead = await _leads.GetAsync(id);
        if (!Lead.CanConvert)
        {
            Toast("Only qualified leads can be converted.");
            return RedirectToPage("Details", new { id });
        }
        var draft = await _leads.ConvertDraftAsync(id);
        Input = new Form
        {
            Name = draft.Customer.Name, Email = draft.Customer.Email, Phone = draft.Customer.Phone, Company = draft.Customer.Company,
            DealName = draft.DealName, Amount = draft.Amount > 0 ? draft.Amount : null, Probability = draft.Probability, CloseDate = draft.CloseDate
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ConvertResult? result = null;
        var ok = await TryAsync(async () =>
        {
            var customer = new CustomerDetails(Input.Name, Input.Email, Input.Phone, Input.Company, null, null, null, CustomerStatus.Active, null);
            result = await _leads.ConvertAsync(id, new ConvertLead(customer, Input.DealName, Input.Amount ?? 0, Input.Probability ?? 0, Input.CloseDate ?? default));
        });
        if (!ok)
        {
            Lead = await _leads.GetAsync(id);
            return Page();
        }
        Toast(result!.NewCustomer ? "Converted - new customer and opportunity created." : "Converted - linked to the existing customer.");
        return RedirectToPage("/Opportunities/Details", new { id = result.OpportunityId });
    }
}
