using Acxiom.Application.Common;
using Acxiom.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Auditing;

public class AuditFilter : ListFilter
{
    public string? UserId { get; set; }
    public string? Entity { get; set; }
    public string? Act { get; set; }
    public bool? FailedOnly { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class AuditQuery
{
    private readonly IAppDbContext _db;

    public AuditQuery(IAppDbContext db) => _db = db;

    public Task<Paged<AuditEntry>> SearchAsync(AuditFilter f)
    {
        var text = f.Text;
        return _db.AuditEntries.AsNoTracking()
            .WhereIf(!string.IsNullOrEmpty(f.UserId), e => e.UserId == f.UserId)
            .WhereIf(!string.IsNullOrEmpty(f.Entity), e => e.EntityName == f.Entity)
            .WhereIf(!string.IsNullOrEmpty(f.Act), e => e.Action == f.Act)
            .WhereIf(f.FailedOnly == true, e => !e.Succeeded)
            .WhereIf(f.From != null, e => e.CreatedDate >= f.From!.Value.Date)
            .WhereIf(f.To != null, e => e.CreatedDate < f.To!.Value.Date.AddDays(1))
            .WhereIf(text != null, e => (e.UserName != null && e.UserName.ToLower().Contains(text!)) || e.RecordId == f.Q
                                        || (e.Note != null && e.Note.ToLower().Contains(text!)))
            .OrderByDescending(e => e.Id)
            .PageAsync(f);
    }

    /// <summary>History of one record, newest first (shown on details pages).</summary>
    public Task<List<AuditEntry>> HistoryAsync(string entity, int id) =>
        _db.AuditEntries.AsNoTracking()
            .Where(e => e.EntityName == entity && e.RecordId == id.ToString())
            .OrderByDescending(e => e.Id).Take(30).ToListAsync();
}
