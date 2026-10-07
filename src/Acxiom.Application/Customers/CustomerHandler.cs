using System.Linq.Expressions;
using Acxiom.Application.Common;
using Acxiom.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Customers;

public class CustomerFilter : ListFilter
{
    public CustomerStatus? Status { get; set; }
}

public class CustomerRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public CustomerStatus Status { get; set; }
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CustomerView : CustomerRow
{
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record Option(int Id, string Text);

public class CustomerHandler
{
    private static readonly Dictionary<string, Expression<Func<Customer, object?>>> SortColumns = new()
    {
        ["code"] = c => c.Code,
        ["name"] = c => c.Name,
        ["company"] = c => c.Company,
        ["email"] = c => c.Email,
        ["status"] = c => c.Status,
        ["created"] = c => c.CreatedAt,
    };

    private readonly IAppDbContext _db;
    private readonly Owners _owners;

    public CustomerHandler(IAppDbContext db, Owners owners)
    {
        _db = db;
        _owners = owners;
    }

    public async Task<Paged<CustomerRow>> SearchAsync(CustomerFilter f)
    {
        var text = f.Text;
        var query = _db.Customers.AsNoTracking()
            .WhereIf(text != null, c => c.Name.ToLower().Contains(text!) || c.Email.Contains(text!) || c.Phone.Contains(text!)
                                        || c.Code.ToLower().Contains(text!) || (c.Company != null && c.Company.ToLower().Contains(text!)))
            .WhereIf(f.Status != null, c => c.Status == f.Status)
            .WhereIf(!string.IsNullOrEmpty(f.Owner), c => c.OwnerId == f.Owner)
            .SortBy(f.Sort, SortColumns, "-created");

        var page = await query.Select(c => new CustomerRow
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Company = c.Company, Email = c.Email, Phone = c.Phone,
            Status = c.Status, OwnerId = c.OwnerId, CreatedAt = c.CreatedAt
        }).PageAsync(f);

        await _owners.FillNamesAsync(page.Items, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return page;
    }

    public async Task<CustomerView> GetAsync(int id)
    {
        var view = await _db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => new CustomerView
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Company = c.Company, Email = c.Email, Phone = c.Phone,
            Status = c.Status, OwnerId = c.OwnerId, CreatedAt = c.CreatedAt, Address = c.Address, City = c.City,
            State = c.State, Notes = c.Notes, UpdatedAt = c.UpdatedAt
        }).FirstOrDefaultAsync() ?? throw new NotFoundException("Customer");

        await _owners.FillNamesAsync(new[] { view }, r => r.OwnerId, (r, n) => r.OwnerName = n);
        return view;
    }

    public async Task<int> CreateAsync(CustomerDetails details, string? ownerId)
    {
        var owner = await _owners.PickAsync(ownerId);
        var customer = Customer.Create(await NextCodeAsync(), details, owner);
        await EnsureNotDuplicateAsync(customer, 0);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer.Id;
    }

    public async Task UpdateAsync(int id, CustomerDetails details, string? ownerId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id) ?? throw new NotFoundException("Customer");
        customer.Update(details);
        customer.AssignTo(await _owners.PickAsync(ownerId, customer.OwnerId));
        await EnsureNotDuplicateAsync(customer, id);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        _owners.EnsureCanDelete();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id) ?? throw new NotFoundException("Customer");

        var hasDeals = await _db.Opportunities.IgnoreQueryFilters().AnyAsync(o => o.CustomerId == id && !o.IsDeleted);
        if (hasDeals) throw new ConflictException("This customer still has opportunities. Remove those first.");

        customer.Delete();
        await _db.SaveChangesAsync();
    }

    public Task<List<Option>> OptionsAsync() =>
        _db.Customers.AsNoTracking().OrderBy(c => c.Name).Select(c => new Option(c.Id, c.Name + " - " + c.Code)).ToListAsync();

    internal async Task<string> NextCodeAsync()
    {
        // codes are never reused - soft deleted rows still count
        var count = await _db.Customers.IgnoreQueryFilters().CountAsync();
        return $"CUST-{count + 1:D4}";
    }

    internal async Task EnsureNotDuplicateAsync(Customer c, int selfId)
    {
        // check against every live customer, not just the ones this user can see
        var others = _db.Customers.IgnoreQueryFilters().Where(x => !x.IsDeleted && x.Id != selfId);
        if (await others.AnyAsync(x => x.Email == c.Email))
            throw new ConflictException("Another customer already uses this email.", "Email");
        if (await others.AnyAsync(x => x.Phone == c.Phone))
            throw new ConflictException("Another customer already uses this phone number.", "Phone");

        var name = c.Name.ToLower();
        var company = c.Company?.ToLower();
        if (company != null && await others.AnyAsync(x => x.Name.ToLower() == name && x.Company != null && x.Company.ToLower() == company))
            throw new ConflictException($"{c.Name} at {c.Company} is already a customer.", "Name");
    }
}
