namespace Acxiom.Domain.Auditing;

/// <summary>One row in the audit trail. Rows are only ever inserted.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? Note { get; set; }
    public string? IpAddress { get; set; }
}
