using Acxiom.Domain.Common;

namespace Acxiom.Domain.Customers;

public enum CustomerStatus { Active, Prospect, Inactive }

public record CustomerDetails(
    string? Name, string? Email, string? Phone, string? Company,
    string? Address, string? City, string? State, CustomerStatus Status, string? Notes);

public class Customer : OwnedEntity
{
    private Customer() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string? Company { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public CustomerStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public static Customer Create(string code, CustomerDetails details, string ownerId)
    {
        var c = new Customer { Code = code };
        c.Update(details);
        c.AssignTo(ownerId);
        return c;
    }

    public void Update(CustomerDetails d)
    {
        Name = Rules.Require(d.Name, nameof(Name), "Customer Name", Rules.NameMax);
        Email = Rules.Email(d.Email);
        Phone = Rules.Phone(d.Phone);
        Company = Rules.Optional(d.Company, nameof(Company), "Company", Rules.CompanyMax);
        Address = Rules.Optional(d.Address, nameof(Address), "Address", 250);
        City = Rules.Optional(d.City, nameof(City), "City", 60);
        State = Rules.Optional(d.State, nameof(State), "State", 60);
        Notes = Rules.Optional(d.Notes, nameof(Notes), "Notes", Rules.NotesMax);
        if (!Enum.IsDefined(d.Status)) throw new DomainException("Pick a valid status.", nameof(Status));
        Status = d.Status;
    }
}
