using System.Security.Claims;
using Acxiom.Application.Common;
using Acxiom.Domain;
using Acxiom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acxiom.Infrastructure.Identity;

public static class TeamScope
{
    /// <summary>The user themself plus, for managers, everyone in the teams they manage.</summary>
    public static async Task<List<string>> OwnerIdsAsync(CrmDbContext db, AppUser user, bool isManager)
    {
        var ids = new List<string> { user.Id };
        if (!isManager) return ids;

        var teamIds = await db.Teams.Where(t => t.ManagerId == user.Id).Select(t => t.Id).ToListAsync();
        if (teamIds.Count == 0) return ids;

        var members = await db.Users.Where(u => u.TeamId != null && teamIds.Contains(u.TeamId.Value) && u.Id != user.Id)
            .Select(u => u.Id).ToListAsync();
        ids.AddRange(members);
        return ids;
    }
}

/// <summary>
/// Adds the user's name and the list of record owners they can see to the cookie / token.
/// Identity rebuilds the principal when the security stamp is re-validated, so team changes
/// reach existing sessions within a minute.
/// </summary>
public class CrmClaimsFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    private readonly CrmDbContext _db;

    public CrmClaimsFactory(UserManager<AppUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options, CrmDbContext db)
        : base(users, roles, options)
    {
        _db = db;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(CrmClaims.FullName, user.FullName));

        var isManager = await UserManager.IsInRoleAsync(user, Roles.Manager);
        var owners = await TeamScope.OwnerIdsAsync(_db, user, isManager);
        identity.AddClaim(new Claim(CrmClaims.Owners, string.Join(' ', owners)));
        return identity;
    }
}
