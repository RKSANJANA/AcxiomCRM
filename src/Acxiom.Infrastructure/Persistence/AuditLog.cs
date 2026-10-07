using Acxiom.Application;
using Acxiom.Domain.Auditing;

namespace Acxiom.Infrastructure.Persistence;

/// <summary>For events the interceptor can't see: logins, conversions, user administration.</summary>
public class AuditLog : IAuditLog
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AuditLog(CrmDbContext db, ICurrentUser user, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task WriteAsync(string action, string entity, string? recordId = null, string? note = null,
        bool succeeded = true, string? userId = null, string? userName = null)
    {
        _db.AuditEntries.Add(new AuditEntry
        {
            CreatedDate = _clock.GetLocalNow().DateTime,
            UserId = userId ?? _user.Id,
            UserName = userName ?? _user.Name,
            IpAddress = _user.IpAddress,
            Action = action,
            EntityName = entity,
            RecordId = recordId,
            Note = note is { Length: > 500 } ? note[..500] : note,
            Succeeded = succeeded
        });
        await _db.SaveChangesAsync();
    }
}
