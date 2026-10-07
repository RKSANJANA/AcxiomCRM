using System.Text.Json;
using System.Text.Json.Serialization;
using Acxiom.Application;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Acxiom.Infrastructure.Persistence;

/// <summary>
/// Hooks into SaveChanges: stamps created/updated info on CRM records and writes an audit row
/// for every insert, update and soft delete with only the fields that actually changed.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    private static readonly HashSet<string> Skip = new() { "CreatedById", "CreatedAt", "UpdatedAt" };

    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private List<(EntityEntry Entry, AuditEntry Row)> _pending = new();
    private bool _writing;

    public AuditInterceptor(ICurrentUser user, TimeProvider clock)
    {
        _user = user;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Flush(eventData.Context!)) eventData.Context!.SaveChanges();
        _writing = false;
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (Flush(eventData.Context!)) await eventData.Context!.SaveChangesAsync(cancellationToken);
        _writing = false;
        return result;
    }

    private void Capture(DbContext ctx)
    {
        if (_writing) return;

        if (ctx.ChangeTracker.Entries<AuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit entries are read-only.");

        var now = _clock.GetLocalNow().DateTime;
        var userId = _user.Id ?? "system";
        _pending = new();

        foreach (var entry in ctx.ChangeTracker.Entries<OwnedEntity>().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                // the seeder sets back-dated stamps itself
                if (entry.Entity.CreatedById == "") entry.Entity.MarkCreated(userId, now);
                Queue(entry, "Create", null, Snapshot(entry), null);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkUpdated(now);
                var changed = entry.Properties.Where(p => p.IsModified && !Skip.Contains(p.Metadata.Name)
                                                          && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                if (changed.Count == 0) continue;

                var deleted = changed.Any(p => p.Metadata.Name == nameof(OwnedEntity.IsDeleted) && p.CurrentValue is true);
                var statusChange = changed.FirstOrDefault(p => p.Metadata.Name is "Status" or "Stage");
                var note = statusChange == null ? null : $"{statusChange.Metadata.Name}: {statusChange.OriginalValue} -> {statusChange.CurrentValue}";

                Queue(entry, deleted ? "Delete" : "Update",
                    JsonSerializer.Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue), Json),
                    JsonSerializer.Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue), Json),
                    note);
            }
        }
    }

    private void Queue(EntityEntry entry, string action, string? oldValue, string? newValue, string? note)
    {
        if (_user.IsSystem) return; // don't audit seed data
        _pending.Add((entry, new AuditEntry
        {
            CreatedDate = _clock.GetLocalNow().DateTime,
            UserId = _user.Id,
            UserName = _user.Name,
            IpAddress = _user.IpAddress,
            Action = action,
            EntityName = entry.Metadata.ClrType.Name,
            OldValue = oldValue,
            NewValue = newValue,
            Note = note
        }));
    }

    // ids of new rows only exist after the first save, so the audit rows go in a second save
    private bool Flush(DbContext ctx)
    {
        if (_writing || _pending.Count == 0) return false;
        _writing = true;
        foreach (var (entry, row) in _pending)
        {
            row.RecordId = entry.Property(nameof(OwnedEntity.Id)).CurrentValue?.ToString();
            ctx.Set<AuditEntry>().Add(row);
        }
        _pending = new();
        return true;
    }

    private static string Snapshot(EntityEntry entry) =>
        JsonSerializer.Serialize(entry.Properties
            .Where(p => !Skip.Contains(p.Metadata.Name) && p.Metadata.Name != "Id" && p.CurrentValue != null)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue), Json);
}
