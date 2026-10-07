namespace Acxiom.Domain.Common;

/// <summary>
/// Base for every CRM record. Each record belongs to a user (the "owner", stored in the
/// AssignedTo column) and is soft deleted. Created/updated stamps are filled in by the
/// persistence layer when the record is saved.
/// </summary>
public abstract class OwnedEntity
{
    public int Id { get; protected set; }

    public string OwnerId { get; protected set; } = "";
    public string CreatedById { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public void MarkCreated(string userId, DateTime now)
    {
        CreatedById = userId;
        CreatedAt = now;
    }

    public void MarkUpdated(DateTime now) => UpdatedAt = now;

    public void AssignTo(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new DomainException("A record must be assigned to someone.", "OwnerId");
        OwnerId = ownerId;
    }

    public virtual void Delete() => IsDeleted = true;
}
