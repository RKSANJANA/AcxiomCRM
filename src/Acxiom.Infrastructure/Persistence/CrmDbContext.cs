using Acxiom.Application;
using Acxiom.Domain.Activities;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Domain.Teams;
using Acxiom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Infrastructure.Persistence;

public class CrmDbContext : IdentityDbContext<AppUser>, IAppDbContext
{
    private readonly ICurrentUser _user;

    public CrmDbContext(DbContextOptions<CrmDbContext> options, ICurrentUser user) : base(options)
    {
        _user = user;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // read by the query filters on every query (EF parameterises context members per instance)
    public bool ScopeAll => _user.SeesEverything;
    public List<string> ScopeOwners => _user.OwnerIds.ToList();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // sqlite has no decimal column type; REAL keeps sorting/filtering in the database
        builder.Properties<decimal>().HaveConversion<double>();
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);

        Scope<Customer>(builder);
        Scope<Lead>(builder);
        Scope<Opportunity>(builder);
        Scope<FollowUp>(builder);
        Scope<Activity>(builder);
    }

    // soft-deleted rows are hidden everywhere; non-admins only see their own (or their team's) rows
    private void Scope<T>(ModelBuilder builder) where T : OwnedEntity =>
        builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (ScopeAll || ScopeOwners.Contains(e.OwnerId)));
}
