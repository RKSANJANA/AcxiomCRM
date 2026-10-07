using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Application.Users;
using Acxiom.Domain;
using Acxiom.Domain.Common;
using Acxiom.Domain.Teams;
using Acxiom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Infrastructure.Identity;

public class UserAdmin : IUserAdmin
{
    private readonly UserManager<AppUser> _users;
    private readonly CrmDbContext _db;
    private readonly IAuditLog _audit;
    private readonly ICurrentUser _me;
    private readonly TimeProvider _clock;

    public UserAdmin(UserManager<AppUser> users, CrmDbContext db, IAuditLog audit, ICurrentUser me, TimeProvider clock)
    {
        _users = users;
        _db = db;
        _audit = audit;
        _me = me;
        _clock = clock;
    }

    public async Task<List<UserSummary>> ListAsync(string? text, string? role, string? state)
    {
        var roles = await RoleMapAsync();
        var teams = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var t = text.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(t) || u.Email!.ToLower().Contains(t));
        }

        var now = DateTimeOffset.UtcNow;
        var list = (await q.OrderBy(u => u.FullName).ToListAsync()).Select(u => new UserSummary
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email ?? "", Role = roles.GetValueOrDefault(u.Id, ""), TeamId = u.TeamId,
            TeamName = u.TeamId != null ? teams.GetValueOrDefault(u.TeamId.Value) : null, IsActive = u.IsActive,
            LockoutEnd = u.LockoutEnd, LockedOut = u.LockoutEnd > now, FailedLogins = u.AccessFailedCount, CreatedAt = u.CreatedAt
        });

        if (!string.IsNullOrEmpty(role)) list = list.Where(u => u.Role == role);
        list = state switch
        {
            "active" => list.Where(u => u.IsActive),
            "inactive" => list.Where(u => !u.IsActive),
            "locked" => list.Where(u => u.LockedOut),
            _ => list
        };
        return list.ToList();
    }

    public async Task<UserSummary?> FindAsync(string id) => (await ListAsync(null, null, null)).FirstOrDefault(u => u.Id == id);

    public async Task<string> CreateAsync(SaveUser input)
    {
        CheckRole(input.Role);
        if (string.IsNullOrWhiteSpace(input.Password)) throw new DomainException("Password is required.", "Password");
        var email = Rules.Email(input.Email);
        if (await _users.FindByEmailAsync(email) != null) throw new ConflictException("That email is already registered.", "Email");

        var user = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true,
            FullName = Rules.Require(input.FullName, "FullName", "Full name", 100),
            IsActive = input.IsActive, TeamId = input.TeamId, CreatedAt = _clock.GetLocalNow().DateTime
        };
        Check(await _users.CreateAsync(user, input.Password), "Password");
        await _users.AddToRoleAsync(user, input.Role);
        await _audit.WriteAsync("Create", "User", user.Id, $"{email} as {input.Role}");
        return user.Id;
    }

    public async Task UpdateAsync(string id, SaveUser input)
    {
        CheckRole(input.Role);
        var user = await _users.FindByIdAsync(id) ?? throw new NotFoundException("User");
        var oldRole = (await _users.GetRolesAsync(user)).FirstOrDefault();
        var self = id == _me.Id;
        if (self && oldRole != input.Role) throw new DomainException("You can't change your own role.", "Role");
        if (self && !input.IsActive) throw new DomainException("You can't deactivate yourself.", "IsActive");

        var email = Rules.Email(input.Email);
        var clash = await _users.FindByEmailAsync(email);
        if (clash != null && clash.Id != id) throw new ConflictException("That email is already registered.", "Email");

        var wasActive = user.IsActive;
        var oldTeam = user.TeamId;
        user.FullName = Rules.Require(input.FullName, "FullName", "Full name", 100);
        user.Email = email;
        user.UserName = email;
        user.IsActive = input.IsActive;
        user.TeamId = input.TeamId;
        Check(await _users.UpdateAsync(user), "");

        if (oldRole != input.Role)
        {
            if (oldRole != null) await _users.RemoveFromRoleAsync(user, oldRole);
            await _users.AddToRoleAsync(user, input.Role);
            await _audit.WriteAsync("RoleChange", "User", id, $"{oldRole ?? "none"} -> {input.Role}");
        }
        if (wasActive != user.IsActive) await _audit.WriteAsync(user.IsActive ? "Activate" : "Deactivate", "User", id);
        await _audit.WriteAsync("Update", "User", id, $"{user.Email}{(oldTeam != user.TeamId ? ", team changed" : "")}");

        // role / status changes should kick existing sessions. Team changes don't need this: the
        // claims (incl. the manager's team list) are rebuilt on the 1-minute stamp validation anyway.
        if (oldRole != input.Role || wasActive != user.IsActive) await _users.UpdateSecurityStampAsync(user);
    }

    public async Task ResetPasswordAsync(string id, string newPassword)
    {
        var user = await _users.FindByIdAsync(id) ?? throw new NotFoundException("User");
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        Check(await _users.ResetPasswordAsync(user, token, newPassword), "NewPassword");
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.WriteAsync("PasswordReset", "User", id, "reset by admin");
    }

    public async Task UnlockAsync(string id)
    {
        var user = await _users.FindByIdAsync(id) ?? throw new NotFoundException("User");
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.WriteAsync("Unlock", "User", id);
    }

    public async Task SetActiveAsync(string id, bool active)
    {
        if (id == _me.Id && !active) throw new DomainException("You can't deactivate yourself.");
        var user = await _users.FindByIdAsync(id) ?? throw new NotFoundException("User");
        if (user.IsActive == active) return;
        user.IsActive = active;
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        await _audit.WriteAsync(active ? "Activate" : "Deactivate", "User", id);
    }

    public async Task<List<TeamSummary>> TeamsAsync()
    {
        var teams = await _db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        var names = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName);
        var counts = await _db.Users.Where(u => u.TeamId != null).GroupBy(u => u.TeamId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return teams.Select(t => new TeamSummary(t.Id, t.Name, t.ManagerId,
            t.ManagerId != null ? names.GetValueOrDefault(t.ManagerId) : null, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task SaveTeamAsync(int? id, string name, string? managerId)
    {
        if (!string.IsNullOrEmpty(managerId) && !(await ManagersAsync()).Any(m => m.Id == managerId))
            throw new DomainException("The team manager has to be an active user with the Manager role.", "ManagerId");
        managerId = string.IsNullOrEmpty(managerId) ? null : managerId;

        var duplicate = await _db.Teams.AnyAsync(t => t.Name == name.Trim() && t.Id != id);
        if (duplicate) throw new ConflictException("A team with that name already exists.", "Name");

        string? previousManager = null;
        if (id == null)
        {
            _db.Teams.Add(Team.Create(name, managerId));
        }
        else
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Team");
            previousManager = team.ManagerId;
            team.Rename(name);
            team.SetManager(managerId);
        }
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(id == null ? "Create" : "Update", "Team", id?.ToString(), $"{name} (manager changed: {previousManager != managerId})");

    }

    public async Task DeleteTeamAsync(int id)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Team");
        await _db.Users.Where(u => u.TeamId == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.TeamId, (int?)null));
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync("Delete", "Team", id.ToString(), team.Name);
    }

    public async Task<List<UserOption>> ManagersAsync()
    {
        var managers = await _users.GetUsersInRoleAsync(Roles.Manager);
        return managers.Where(m => m.IsActive).OrderBy(m => m.FullName).Select(m => new UserOption(m.Id, m.FullName)).ToList();
    }

    public async Task<Dictionary<string, int>> RoleCountsAsync()
    {
        var map = await RoleMapAsync();
        return Roles.All.ToDictionary(r => r, r => map.Values.Count(v => v == r));
    }

    public async Task<SecurityStats> StatsAsync()
    {
        var users = await _db.Users.AsNoTracking().Select(u => new { u.IsActive, u.LockoutEnd }).ToListAsync();
        var today = _clock.GetLocalNow().Date;
        var failed = await _db.AuditEntries.CountAsync(a => a.Action == "LoginFailed" && a.CreatedDate >= today);
        var now = DateTimeOffset.UtcNow;
        return new SecurityStats(users.Count, users.Count(u => u.IsActive), users.Count(u => u.LockoutEnd > now), failed);
    }

    private async Task<Dictionary<string, string>> RoleMapAsync() =>
        await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name })
            .ToDictionaryAsync(x => x.UserId, x => x.Name!);

    private static void CheckRole(string role)
    {
        if (!Roles.All.Contains(role)) throw new DomainException("Pick a valid role.", "Role");
    }

    private static void Check(IdentityResult result, string field)
    {
        if (!result.Succeeded) throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)), field);
    }
}
