namespace Acxiom.Application.Users;

public class UserSummary
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsActive { get; set; }
    public bool LockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int FailedLogins { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record SaveUser(string FullName, string Email, string Role, int? TeamId, bool IsActive, string? Password);

public record TeamSummary(int Id, string Name, string? ManagerId, string? ManagerName, int Members);

public record SecurityStats(int Users, int ActiveUsers, int LockedUsers, int FailedLoginsToday);

/// <summary>User, role and team administration (admin only). Implemented on top of ASP.NET Core Identity.</summary>
public interface IUserAdmin
{
    Task<List<UserSummary>> ListAsync(string? text, string? role, string? state);
    Task<UserSummary?> FindAsync(string id);
    Task<string> CreateAsync(SaveUser user);
    Task UpdateAsync(string id, SaveUser user);
    Task ResetPasswordAsync(string id, string newPassword);
    Task UnlockAsync(string id);
    Task SetActiveAsync(string id, bool active);

    Task<List<TeamSummary>> TeamsAsync();
    Task SaveTeamAsync(int? id, string name, string? managerId);
    Task DeleteTeamAsync(int id);
    Task<List<UserOption>> ManagersAsync();

    Task<Dictionary<string, int>> RoleCountsAsync();
    Task<SecurityStats> StatsAsync();
}
