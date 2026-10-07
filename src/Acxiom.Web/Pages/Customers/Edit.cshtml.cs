using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Customers;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Customers;

public class EditModel : CrmPage
{
    private readonly CustomerHandler _customers;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public EditModel(CustomerHandler customers, IUserDirectory users, ICurrentUser me)
    {
        _customers = customers;
        _users = users;
        _me = me;
    }

    public class Form
    {
        [Required(ErrorMessage = "Customer Name is required."), StringLength(Rules.NameMax), Display(Name = "Customer name")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Email is required."), StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Phone is required."), RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.Messages.Phone)]
        [Display(Name = "Mobile number")]
        public string? Phone { get; set; }

        [StringLength(Rules.CompanyMax)] public string? Company { get; set; }
        [StringLength(250)] public string? Address { get; set; }
        [StringLength(60)] public string? City { get; set; }
        [StringLength(60)] public string? State { get; set; }
        [Required] public CustomerStatus Status { get; set; } = CustomerStatus.Active;
        [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
        [Display(Name = "Owner")] public string? OwnerId { get; set; }

        public CustomerDetails ToDetails() => new(Name, Email, Phone, Company, Address, City, State, Status, Notes);
    }

    [BindProperty] public Form Input { get; set; } = new();
    public int? Id { get; private set; }
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task OnGetAsync(int? id)
    {
        Id = id;
        if (id != null)
        {
            var c = await _customers.GetAsync(id.Value);
            Input = new Form
            {
                Name = c.Name, Email = c.Email, Phone = c.Phone, Company = c.Company, Address = c.Address, City = c.City,
                State = c.State, Status = c.Status, Notes = c.Notes, OwnerId = c.OwnerId
            };
        }
        else Input.OwnerId = _me.Id;
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        var newId = id;
        var ok = await TryAsync(async () =>
        {
            if (id == null) newId = await _customers.CreateAsync(Input.ToDetails(), Input.OwnerId);
            else await _customers.UpdateAsync(id.Value, Input.ToDetails(), Input.OwnerId);
        });
        if (!ok)
        {
            await LoadListsAsync();
            return Page();
        }
        Toast(id == null ? "Customer added." : "Changes saved.");
        return RedirectToPage("Details", new { id = newId });
    }

    private async Task LoadListsAsync()
    {
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
    }
}
