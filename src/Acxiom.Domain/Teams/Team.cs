using Acxiom.Domain.Common;

namespace Acxiom.Domain.Teams;

/// <summary>A sales team. Members are linked through AppUser.TeamId; the manager sees the team's records.</summary>
public class Team
{
    private Team() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? ManagerId { get; private set; }

    public static Team Create(string? name, string? managerId)
    {
        var t = new Team();
        t.Rename(name);
        t.ManagerId = managerId;
        return t;
    }

    public void Rename(string? name) => Name = Rules.Require(name, nameof(Name), "Team name", 80);

    public void SetManager(string? managerId) => ManagerId = managerId;
}
