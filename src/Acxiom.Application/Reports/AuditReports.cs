using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Reports;

public class UserActivityReport : ReportBase
{
    private readonly ICurrentUser _me;

    public UserActivityReport(IAppDbContext db, IUserDirectory users, TimeProvider clock, ICurrentUser me) : base(db, users, clock)
    {
        _me = me;
    }

    public override string Slug => "user-activity";
    public override string Title => "User activity";
    public override string Summary => "What each user has been doing, counted from the audit trail.";
    public override bool AllowedFor(ICurrentUser user) => user.IsAdmin || user.IsManager;

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var q = Db.AuditEntries.AsNoTracking().Where(e => e.UserId != null);
        if (!_me.SeesEverything)
        {
            var ids = _me.OwnerIds.ToList();
            q = q.Where(e => ids.Contains(e.UserId!));
        }
        if (a.Owner != null) q = q.Where(e => e.UserId == a.Owner);

        var entries = (await q.Select(e => new { e.UserId, e.Action, e.CreatedDate }).ToListAsync())
            .Where(e => a.InRange(e.CreatedDate)).ToList();
        var name = await NameLookup(entries.Select(e => e.UserId!));

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("User"), new Column("Logins", ColumnKind.Number), new Column("Created", ColumnKind.Number), new Column("Updated", ColumnKind.Number), new Column("Deleted", ColumnKind.Number), new Column("All actions", ColumnKind.Number), new Column("Last seen", ColumnKind.Date) });
        foreach (var g in entries.GroupBy(e => e.UserId!).OrderByDescending(g => g.Count()))
        {
            int Count(string action) => g.Count(e => e.Action == action);
            t.Rows.Add(new object?[] { name(g.Key), Count("Login"), Count("Create"), Count("Update"), Count("Delete"), g.Count(), g.Max(e => e.CreatedDate) });
        }
        t.Totals.Add(("Users", t.Rows.Count.ToString()));
        t.Totals.Add(("Actions", entries.Count.ToString()));
        return t;
    }
}

public class AuditReport : ReportBase
{
    public AuditReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }

    public override string Slug => "audit";
    public override string Title => "Audit events";
    public override string Summary => "Security and business events from the audit trail.";
    public override bool AllowedFor(ICurrentUser user) => user.IsAdmin;
    public override (string Label, string[] Values)? OptionFilter =>
        ("Action", new[] { "Login", "LoginFailed", "Lockout", "Logout", "Register", "Create", "Update", "Delete", "Convert", "RoleChange", "PasswordReset", "Unlock", "Deactivate", "Activate" });

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var q = Db.AuditEntries.AsNoTracking();
        if (a.Option != null) q = q.Where(e => e.Action == a.Option);
        if (a.From != null) q = q.Where(e => e.CreatedDate >= a.From.Value.Date);
        if (a.To != null) q = q.Where(e => e.CreatedDate < a.To.Value.Date.AddDays(1));
        var data = await q.OrderByDescending(e => e.Id).Take(5000).ToListAsync();

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("When", ColumnKind.Date), new Column("User"), new Column("Action"), new Column("Entity"), new Column("Record"), new Column("Result", ColumnKind.Badge), new Column("IP"), new Column("Note") });
        foreach (var e in data)
            t.Rows.Add(new object?[] { e.CreatedDate, e.UserName, e.Action, e.EntityName, e.RecordId, e.Succeeded ? "OK" : "Failed", e.IpAddress, e.Note });
        t.Totals.Add(("Events", data.Count.ToString()));
        t.Totals.Add(("Failures", data.Count(e => !e.Succeeded).ToString()));
        return t;
    }
}
