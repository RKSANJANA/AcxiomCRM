using Acxiom.Application;
using Acxiom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Infrastructure.Identity;

public class UserDirectory : IUserDirectory
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _me;

    public UserDirectory(CrmDbContext db, ICurrentUser me)
    {
        _db = db;
        _me = me;
    }

    public async Task<IReadOnlyDictionary<string, string>> NamesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();
        return await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public async Task<IReadOnlyList<UserOption>> AssignableAsync()
    {
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!_me.SeesEverything)
        {
            var ids = _me.OwnerIds.ToList();
            q = q.Where(u => ids.Contains(u.Id));
        }
        return await q.OrderBy(u => u.FullName).Select(u => new UserOption(u.Id, u.FullName)).ToListAsync();
    }
}
