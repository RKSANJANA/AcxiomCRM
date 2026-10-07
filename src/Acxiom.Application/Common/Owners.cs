using Acxiom.Domain.Common;

namespace Acxiom.Application.Common;

/// <summary>Works out who a record should be assigned to, and fills owner names into list rows.</summary>
public class Owners
{
    private readonly ICurrentUser _me;
    private readonly IUserDirectory _directory;

    public Owners(ICurrentUser me, IUserDirectory directory)
    {
        _me = me;
        _directory = directory;
    }

    public async Task<string> PickAsync(string? requested, string? current = null)
    {
        var me = _me.Id ?? throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(requested)) return current ?? me;
        if (requested == me || requested == current) return requested;

        if (!_me.IsAdmin && !_me.IsManager)
            throw new ForbiddenException("Sales executives can only assign records to themselves.");

        var allowed = await _directory.AssignableAsync();
        if (allowed.All(u => u.Id != requested))
            throw new DomainException("Pick an active user from your team.", "OwnerId");
        return requested;
    }

    public async Task FillNamesAsync<T>(IEnumerable<T> rows, Func<T, string> ownerId, Action<T, string> setName)
    {
        var list = rows.ToList();
        var names = await _directory.NamesAsync(list.Select(ownerId).Distinct());
        foreach (var row in list) setName(row, names.TryGetValue(ownerId(row), out var n) ? n : "-");
    }

    public void EnsureCanDelete()
    {
        if (!_me.CanDelete) throw new ForbiddenException("Only admins and managers can delete records.");
    }
}
