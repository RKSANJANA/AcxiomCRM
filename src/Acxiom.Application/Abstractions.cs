using Acxiom.Domain.Activities;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Acxiom.Application;

/// <summary>
/// The CRM tables. Queries on the CRM sets are automatically limited to what the
/// signed-in user may see (global query filters in the infrastructure layer).
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Opportunity> Opportunities { get; }
    DbSet<FollowUp> FollowUps { get; }
    DbSet<Activity> Activities { get; }
    DbSet<Team> Teams { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    string? Id { get; }
    string? Name { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }

    /// <summary>True for background work with no HTTP request (startup seeding).</summary>
    bool IsSystem { get; }

    bool IsAdmin { get; }
    bool IsManager { get; }

    /// <summary>Owners whose records this user can see. Ignored when <see cref="SeesEverything"/>.</summary>
    IReadOnlyList<string> OwnerIds { get; }

    bool SeesEverything => IsSystem || IsAdmin;
    bool CanDelete => IsAdmin || IsManager;
    bool CanSee(string ownerId) => SeesEverything || OwnerIds.Contains(ownerId);
}

public record UserOption(string Id, string Name);

public interface IUserDirectory
{
    Task<IReadOnlyDictionary<string, string>> NamesAsync(IEnumerable<string> userIds);

    /// <summary>Active users the current user is allowed to assign records to.</summary>
    Task<IReadOnlyList<UserOption>> AssignableAsync();
}

public interface IAuditLog
{
    Task WriteAsync(string action, string entity, string? recordId = null, string? note = null,
        bool succeeded = true, string? userId = null, string? userName = null);
}
