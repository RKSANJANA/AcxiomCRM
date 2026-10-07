namespace Acxiom.Application.Reports;

public enum ColumnKind { Text, Number, Money, Date, Badge }

public record Column(string Header, ColumnKind Kind = ColumnKind.Text);

public class ReportArgs
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Option { get; set; }
    public string? Owner { get; set; }

    public bool InRange(DateTime d) => (From == null || d >= From.Value.Date) && (To == null || d < To.Value.Date.AddDays(1));
}

public class ReportTable
{
    public List<Column> Columns { get; } = new();
    public List<object?[]> Rows { get; } = new();
    public List<(string Label, string Value)> Totals { get; } = new();
}

/// <summary>
/// One report = one class. The Reports page lists every registered IReport the user is allowed to run,
/// and handles filtering by text, sorting, paging and CSV export generically.
/// </summary>
public interface IReport
{
    string Slug { get; }
    string Title { get; }
    string Summary { get; }

    /// <summary>Label + values for the optional dropdown filter (status, stage, action...). Null = no dropdown.</summary>
    (string Label, string[] Values)? OptionFilter { get; }

    bool AllowedFor(ICurrentUser user);

    Task<ReportTable> BuildAsync(ReportArgs args);
}
