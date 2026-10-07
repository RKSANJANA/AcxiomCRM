using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Reports;

public abstract class ReportBase : IReport
{
    protected readonly IAppDbContext Db;
    protected readonly IUserDirectory Users;
    protected readonly TimeProvider Clock;

    protected ReportBase(IAppDbContext db, IUserDirectory users, TimeProvider clock)
    {
        Db = db;
        Users = users;
        Clock = clock;
    }

    public abstract string Slug { get; }
    public abstract string Title { get; }
    public abstract string Summary { get; }
    public virtual (string Label, string[] Values)? OptionFilter => null;
    public virtual bool AllowedFor(ICurrentUser user) => true;
    public abstract Task<ReportTable> BuildAsync(ReportArgs args);

    protected DateTime Today => Clock.GetLocalNow().Date;

    protected async Task<Func<string, string>> NameLookup(IEnumerable<string> ids)
    {
        var names = await Users.NamesAsync(ids.Distinct());
        return id => names.TryGetValue(id, out var n) ? n : "-";
    }

    protected static string Percent(int part, int whole) => whole == 0 ? "-" : $"{part * 100.0 / whole:0.#}%";
    protected static string Rupees(decimal v) => v.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-IN"));
}

public class CustomerReport : ReportBase
{
    public CustomerReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "customers";
    public override string Title => "Customers";
    public override string Summary => "Customer list with status, owner and when they were added.";
    public override (string Label, string[] Values)? OptionFilter => ("Status", Enum.GetNames<CustomerStatus>());

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var data = (await Db.Customers.AsNoTracking().ToListAsync())
            .Where(c => a.InRange(c.CreatedAt) && (a.Option == null || c.Status.ToString() == a.Option) && (a.Owner == null || c.OwnerId == a.Owner))
            .OrderBy(c => c.Name).ToList();
        var name = await NameLookup(data.Select(c => c.OwnerId));

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("Code"), new Column("Customer"), new Column("Company"), new Column("Email"), new Column("Phone"), new Column("Status", ColumnKind.Badge), new Column("Owner"), new Column("Added", ColumnKind.Date) });
        foreach (var c in data) t.Rows.Add(new object?[] { c.Code, c.Name, c.Company, c.Email, c.Phone, c.Status.ToString(), name(c.OwnerId), c.CreatedAt });
        t.Totals.Add(("Customers", data.Count.ToString()));
        foreach (var g in data.GroupBy(c => c.Status)) t.Totals.Add((g.Key.ToString(), g.Count().ToString()));
        return t;
    }
}

public class LeadReport : ReportBase
{
    public LeadReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "leads";
    public override string Title => "Leads";
    public override string Summary => "Source, status, owner and conversion details for each lead.";
    public override (string Label, string[] Values)? OptionFilter => ("Status", Enum.GetNames<LeadStatus>());

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var data = (await Db.Leads.AsNoTracking().ToListAsync())
            .Where(l => a.InRange(l.CreatedAt) && (a.Option == null || l.Status.ToString() == a.Option) && (a.Owner == null || l.OwnerId == a.Owner))
            .OrderByDescending(l => l.CreatedAt).ToList();
        var name = await NameLookup(data.Select(l => l.OwnerId));

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("Code"), new Column("Lead"), new Column("Company"), new Column("Source"), new Column("Status", ColumnKind.Badge), new Column("Expected", ColumnKind.Money), new Column("Owner"), new Column("Created", ColumnKind.Date), new Column("Converted", ColumnKind.Date) });
        foreach (var l in data) t.Rows.Add(new object?[] { l.Code, l.Name, l.Company, l.Source.ToString(), l.Status.ToString(), l.ExpectedValue, name(l.OwnerId), l.CreatedAt, l.ConvertedAt });
        var converted = data.Count(l => l.Status == LeadStatus.Converted);
        t.Totals.Add(("Leads", data.Count.ToString()));
        t.Totals.Add(("Converted", converted.ToString()));
        t.Totals.Add(("Conversion", Percent(converted, data.Count)));
        return t;
    }
}

public class FollowUpReport : ReportBase
{
    public FollowUpReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "followups";
    public override string Title => "Follow-ups";
    public override string Summary => "Planned, completed, missed and overdue follow-ups.";
    public override (string Label, string[] Values)? OptionFilter => ("Status", Enum.GetNames<FollowUpStatus>().Append("Overdue").ToArray());

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var today = Today;
        string StatusOf(FollowUp f) => f.IsOverdue(today) ? "Overdue" : f.Status.ToString();

        var data = (await Db.FollowUps.AsNoTracking().ToListAsync())
            .Where(f => a.InRange(f.DueAt) && (a.Option == null || StatusOf(f) == a.Option) && (a.Owner == null || f.OwnerId == a.Owner))
            .OrderBy(f => f.DueAt).ToList();
        var name = await NameLookup(data.Select(f => f.OwnerId));

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("Due", ColumnKind.Date), new Column("Subject"), new Column("Type"), new Column("For"), new Column("Status", ColumnKind.Badge), new Column("Assigned to"), new Column("Completed", ColumnKind.Date) });
        foreach (var f in data) t.Rows.Add(new object?[] { f.DueAt, f.Subject, f.Type.ToString(), $"{f.Target} #{f.TargetId}", StatusOf(f), name(f.OwnerId), f.CompletedAt });
        foreach (var g in data.GroupBy(StatusOf)) t.Totals.Add((g.Key, g.Count().ToString()));
        return t;
    }
}

public class OpportunityReport : ReportBase
{
    public OpportunityReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "opportunities";
    public override string Title => "Opportunities";
    public override string Summary => "Stage, amount, probability and expected close date.";
    public override (string Label, string[] Values)? OptionFilter => ("Stage", Enum.GetNames<Stage>());

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var data = (await Db.Opportunities.AsNoTracking().ToListAsync())
            .Where(o => a.InRange(o.CreatedAt) && (a.Option == null || o.Stage.ToString() == a.Option) && (a.Owner == null || o.OwnerId == a.Owner))
            .OrderBy(o => o.ExpectedCloseDate).ToList();
        var name = await NameLookup(data.Select(o => o.OwnerId));
        var ids = data.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await Db.Customers.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("Opportunity"), new Column("Customer"), new Column("Stage", ColumnKind.Badge), new Column("Amount", ColumnKind.Money), new Column("Prob. %", ColumnKind.Number), new Column("Weighted", ColumnKind.Money), new Column("Close date", ColumnKind.Date), new Column("Owner") });
        foreach (var o in data)
            t.Rows.Add(new object?[] { o.Name, customers.GetValueOrDefault(o.CustomerId), o.Stage.ToString(), o.Amount, o.Probability, o.Weighted, o.ExpectedCloseDate, name(o.OwnerId) });
        var open = data.Where(o => o.IsOpen).ToList();
        t.Totals.Add(("Deals", data.Count.ToString()));
        t.Totals.Add(("Open value", Rupees(open.Sum(o => o.Amount))));
        t.Totals.Add(("Weighted", Rupees(open.Sum(o => o.Weighted))));
        return t;
    }
}

public class PipelineReport : ReportBase
{
    public PipelineReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "pipeline";
    public override string Title => "Sales pipeline";
    public override string Summary => "Open pipeline per owner and stage, with weighted value.";

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var data = (await Db.Opportunities.AsNoTracking().ToListAsync())
            .Where(o => a.InRange(o.CreatedAt) && (a.Owner == null || o.OwnerId == a.Owner)).ToList();
        var name = await NameLookup(data.Select(o => o.OwnerId));
        var openStages = new[] { Stage.Qualification, Stage.Proposal, Stage.Negotiation };

        var t = new ReportTable();
        t.Columns.Add(new Column("Owner"));
        t.Columns.AddRange(openStages.Select(s => new Column(s.ToString(), ColumnKind.Money)));
        t.Columns.AddRange(new[] { new Column("Open total", ColumnKind.Money), new Column("Weighted", ColumnKind.Money), new Column("Won", ColumnKind.Money) });

        foreach (var g in data.GroupBy(o => o.OwnerId))
        {
            var row = new List<object?> { name(g.Key) };
            row.AddRange(openStages.Select(s => (object)g.Where(o => o.Stage == s).Sum(o => o.Amount)));
            row.Add(g.Where(o => o.IsOpen).Sum(o => o.Amount));
            row.Add(g.Where(o => o.IsOpen).Sum(o => o.Weighted));
            row.Add(g.Where(o => o.Status == OpportunityStatus.Won).Sum(o => o.Amount));
            t.Rows.Add(row.ToArray());
        }
        foreach (var s in Enum.GetValues<Stage>())
            t.Totals.Add((s.ToString(), Rupees(data.Where(o => o.Stage == s).Sum(o => o.Amount))));
        return t;
    }
}

public class ConversionReport : ReportBase
{
    public ConversionReport(IAppDbContext db, IUserDirectory users, TimeProvider clock) : base(db, users, clock) { }
    public override string Slug => "conversion";
    public override string Title => "Sales & conversion";
    public override string Summary => "How many leads convert, by source, and how opportunities end up.";

    public override async Task<ReportTable> BuildAsync(ReportArgs a)
    {
        var leads = (await Db.Leads.AsNoTracking().ToListAsync()).Where(l => a.InRange(l.CreatedAt) && (a.Owner == null || l.OwnerId == a.Owner)).ToList();
        var deals = (await Db.Opportunities.AsNoTracking().ToListAsync()).Where(o => a.InRange(o.CreatedAt) && (a.Owner == null || o.OwnerId == a.Owner)).ToList();

        var t = new ReportTable();
        t.Columns.AddRange(new[] { new Column("Source"), new Column("Leads", ColumnKind.Number), new Column("Converted", ColumnKind.Number), new Column("Lost", ColumnKind.Number), new Column("Rate"), new Column("Deals won", ColumnKind.Number), new Column("Won value", ColumnKind.Money) });
        foreach (var source in Enum.GetValues<LeadSource>())
        {
            var ls = leads.Where(l => l.Source == source).ToList();
            var won = deals.Where(o => o.Source == source && o.Status == OpportunityStatus.Won).ToList();
            if (ls.Count == 0 && won.Count == 0) continue;
            var conv = ls.Count(l => l.Status == LeadStatus.Converted);
            t.Rows.Add(new object?[] { source.ToString(), ls.Count, conv, ls.Count(l => l.Status == LeadStatus.Lost), Percent(conv, ls.Count), won.Count, won.Sum(o => o.Amount) });
        }

        var wonCount = deals.Count(o => o.Status == OpportunityStatus.Won);
        var lostCount = deals.Count(o => o.Status == OpportunityStatus.Lost);
        t.Totals.Add(("Lead conversion", Percent(leads.Count(l => l.Status == LeadStatus.Converted), leads.Count)));
        t.Totals.Add(("Won / lost", $"{wonCount} / {lostCount}"));
        t.Totals.Add(("Win rate", Percent(wonCount, wonCount + lostCount)));
        return t;
    }
}
