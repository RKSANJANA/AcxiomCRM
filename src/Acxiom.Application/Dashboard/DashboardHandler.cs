using Acxiom.Application.Common;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Acxiom.Application.Dashboard;

public enum Period { All, Today, Week, Month, Custom }

public record Series(string Label, IReadOnlyList<decimal> Values);
public record Chart(IReadOnlyList<string> Labels, IReadOnlyList<Series> Series);
public record OwnerFigure(string OwnerId, string Name, int OpenDeals, decimal Pipeline, decimal Won, int Planned);

public class DashboardData
{
    public string PeriodText { get; set; } = "";
    public int Customers { get; set; }
    public int Leads { get; set; }
    public int OpenLeads { get; set; }
    public int Opportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int Won { get; set; }
    public int Lost { get; set; }
    public decimal PipelineValue { get; set; }
    public decimal WeightedPipeline { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }
    public Chart LeadsByStatus { get; set; } = null!;
    public Chart PipelineByStage { get; set; } = null!;
    public Chart MonthlySales { get; set; } = null!;
    public List<OwnerFigure> ByOwner { get; set; } = new();
}

public class DashboardHandler
{
    private readonly IAppDbContext _db;
    private readonly Owners _owners;
    private readonly TimeProvider _clock;

    public DashboardHandler(IAppDbContext db, Owners owners, TimeProvider clock)
    {
        _db = db;
        _owners = owners;
        _clock = clock;
    }

    public static (DateTime? from, DateTime? to, string text) Window(Period period, DateTime today, DateTime? from, DateTime? to)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return period switch
        {
            Period.Today => (today, today.AddDays(1), "Today"),
            Period.Week => (monday, monday.AddDays(7), "This week"),
            Period.Month => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1), "This month"),
            Period.Custom when from != null && to != null && from <= to =>
                (from.Value.Date, to.Value.Date.AddDays(1), $"{from:d MMM yyyy} to {to:d MMM yyyy}"),
            _ => (null, null, "All time")
        };
    }

    public async Task<DashboardData> LoadAsync(Period period, DateTime? from = null, DateTime? to = null)
    {
        var now = _clock.GetLocalNow().DateTime;
        var today = now.Date;
        var (start, end, text) = Window(period, today, from, to);

        bool InWindow(DateTime d) => (start == null || d >= start) && (end == null || d < end);

        // the query filters already limit everything to the user's scope; pull the few
        // columns we need and do the maths here (sqlite can't sum decimals anyway)
        var leads = (await _db.Leads.AsNoTracking().Select(l => new { l.Status, l.CreatedAt }).ToListAsync())
            .Where(l => InWindow(l.CreatedAt)).ToList();
        var deals = (await _db.Opportunities.AsNoTracking()
                .Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, o.CreatedAt, o.ClosedAt, o.OwnerId }).ToListAsync())
            .Where(o => InWindow(o.CreatedAt)).ToList();
        var customerDates = await _db.Customers.AsNoTracking().Select(c => c.CreatedAt).ToListAsync();
        var planned = await _db.FollowUps.AsNoTracking().Where(f => f.Status == FollowUpStatus.Planned)
            .Select(f => new { f.DueAt, f.OwnerId }).ToListAsync();

        var open = deals.Where(d => d.Status == OpportunityStatus.Open).ToList();
        var data = new DashboardData
        {
            PeriodText = text,
            Customers = customerDates.Count(InWindow),
            Leads = leads.Count,
            OpenLeads = leads.Count(l => l.Status is LeadStatus.New or LeadStatus.Contacted or LeadStatus.Qualified),
            Opportunities = deals.Count,
            OpenOpportunities = open.Count,
            Won = deals.Count(d => d.Status == OpportunityStatus.Won),
            Lost = deals.Count(d => d.Status == OpportunityStatus.Lost),
            PipelineValue = open.Sum(d => d.Amount),
            WeightedPipeline = decimal.Round(open.Sum(d => d.Amount * d.Probability / 100m), 2),
            PendingFollowUps = planned.Count,
            OverdueFollowUps = planned.Count(f => f.DueAt < today),
        };

        var statuses = Enum.GetValues<LeadStatus>();
        data.LeadsByStatus = new Chart(statuses.Select(s => s.ToString()).ToList(),
            new[] { new Series("Leads", statuses.Select(s => (decimal)leads.Count(l => l.Status == s)).ToList()) });

        var stages = Enum.GetValues<Stage>();
        data.PipelineByStage = new Chart(stages.Select(s => s.ToString()).ToList(), new[]
        {
            new Series("Amount", stages.Select(s => deals.Where(d => d.Stage == s).Sum(d => d.Amount)).ToList()),
            new Series("Deals", stages.Select(s => (decimal)deals.Count(d => d.Stage == s)).ToList())
        });

        // monthly sales by close month: last 12 months, or the months inside a selected window
        var firstMonth = start != null ? new DateTime(start.Value.Year, start.Value.Month, 1) : new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var lastMonth = end != null ? new DateTime(end.Value.AddDays(-1).Year, end.Value.AddDays(-1).Month, 1) : new DateTime(today.Year, today.Month, 1);
        var closed = await _db.Opportunities.AsNoTracking().Where(o => o.ClosedAt != null)
            .Select(o => new { o.Status, o.Amount, ClosedAt = o.ClosedAt!.Value }).ToListAsync();
        var months = new List<DateTime>();
        for (var m = firstMonth; m <= lastMonth && months.Count < 36; m = m.AddMonths(1)) months.Add(m);
        data.MonthlySales = new Chart(months.Select(m => m.ToString("MMM yy")).ToList(), new[]
        {
            new Series("Won", months.Select(m => closed.Where(c => c.Status == OpportunityStatus.Won && c.ClosedAt.Year == m.Year && c.ClosedAt.Month == m.Month).Sum(c => c.Amount)).ToList()),
            new Series("Lost", months.Select(m => closed.Where(c => c.Status == OpportunityStatus.Lost && c.ClosedAt.Year == m.Year && c.ClosedAt.Month == m.Month).Sum(c => c.Amount)).ToList())
        });

        var byOwner = deals.GroupBy(d => d.OwnerId).Select(g => new OwnerFigure(g.Key, "",
                g.Count(d => d.Status == OpportunityStatus.Open),
                g.Where(d => d.Status == OpportunityStatus.Open).Sum(d => d.Amount),
                g.Where(d => d.Status == OpportunityStatus.Won).Sum(d => d.Amount),
                planned.Count(f => f.OwnerId == g.Key)))
            .OrderByDescending(x => x.Pipeline).ToList();
        var named = new List<OwnerFigure>();
        await _owners.FillNamesAsync(byOwner, x => x.OwnerId, (x, n) => named.Add(x with { Name = n }));
        data.ByOwner = named;

        return data;
    }
}
