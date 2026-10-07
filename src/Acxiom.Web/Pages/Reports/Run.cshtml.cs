using System.Globalization;
using System.Text;
using Acxiom.Application;
using Acxiom.Application.Reports;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.Reports;

public class RunModel : CrmPage
{
    private const int PageSize = 25;
    private readonly IEnumerable<IReport> _reports;
    private readonly IUserDirectory _users;
    private readonly ICurrentUser _me;

    public RunModel(IEnumerable<IReport> reports, IUserDirectory users, ICurrentUser me)
    {
        _reports = reports;
        _users = users;
        _me = me;
    }

    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Option { get; set; }
    [BindProperty(SupportsGet = true)] public string? Owner { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "Page")] public int PageNumber { get; set; } = 1;

    public IReport Report { get; private set; } = null!;
    public ReportTable Table { get; private set; } = null!;
    public List<object?[]> Rows { get; private set; } = new();
    public int Total { get; private set; }
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);
    public IReadOnlyList<UserOption> Owners { get; private set; } = Array.Empty<UserOption>();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var rows = await BuildAsync(slug);
        if (rows == null) return NotFound();
        Total = rows.Count;
        PageNumber = Math.Clamp(PageNumber, 1, Pages);
        Rows = rows.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
        if (_me.IsAdmin || _me.IsManager) Owners = await _users.AssignableAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetCsvAsync(string slug)
    {
        var rows = await BuildAsync(slug);
        if (rows == null) return NotFound();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", Table.Columns.Select(c => Cell(c.Header))));
        foreach (var r in rows) csv.AppendLine(string.Join(",", r.Select(v => Cell(Text(v)))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv",
            $"{slug}-{DateTime.Now:yyyy-MM-dd}.csv");
    }

    public static string Text(object? v) => v switch
    {
        null => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-dd HH:mm"),
        decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };

    private async Task<List<object?[]>?> BuildAsync(string slug)
    {
        var report = _reports.FirstOrDefault(r => r.Slug == slug);
        if (report == null) return null;
        if (!report.AllowedFor(_me)) throw new Acxiom.Application.Common.ForbiddenException();
        Report = report;

        // owner filter can only narrow things down
        var owner = !string.IsNullOrEmpty(Owner) && _me.CanSee(Owner) ? Owner : null;
        Table = await report.BuildAsync(new ReportArgs { From = From, To = To, Option = string.IsNullOrEmpty(Option) ? null : Option, Owner = owner });

        IEnumerable<object?[]> rows = Table.Rows;
        if (!string.IsNullOrWhiteSpace(Q))
            rows = rows.Where(r => r.Any(v => Text(v).Contains(Q.Trim(), StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrEmpty(Sort) && int.TryParse(Sort.TrimStart('-'), out var col) && col >= 0 && col < Table.Columns.Count)
        {
            var desc = Sort.StartsWith('-');
            var cmp = Comparer<object?>.Create((a, b) => a is IComparable x && b != null && a.GetType() == b.GetType()
                ? x.CompareTo(b) : string.Compare(Text(a), Text(b), StringComparison.OrdinalIgnoreCase));
            rows = desc ? rows.OrderByDescending(r => r[col], cmp) : rows.OrderBy(r => r[col], cmp);
        }
        return rows.ToList();
    }

    // quote for CSV and stop spreadsheet apps from treating text as a formula
    private static string Cell(string s)
    {
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
