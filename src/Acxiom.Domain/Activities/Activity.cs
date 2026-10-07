using Acxiom.Domain.Common;
using Acxiom.Domain.FollowUps;

namespace Acxiom.Domain.Activities;

public enum ActivityStatus { Open, Done, Cancelled }

public record ActivityDetails(ContactType Type, string? Subject, string? Description, DateTime When,
    int? CustomerId, int? LeadId, ActivityStatus Status);

/// <summary>A logged call / meeting / email / task, optionally linked to a customer or lead.</summary>
public class Activity : OwnedEntity
{
    private Activity() { }

    public ContactType Type { get; private set; }
    public string Subject { get; private set; } = "";
    public string? Description { get; private set; }
    public DateTime When { get; private set; }
    public int? CustomerId { get; private set; }
    public int? LeadId { get; private set; }
    public ActivityStatus Status { get; private set; }

    public static Activity Log(ActivityDetails details, string ownerId)
    {
        var a = new Activity();
        a.Update(details);
        a.AssignTo(ownerId);
        return a;
    }

    public void Update(ActivityDetails d)
    {
        if (!Enum.IsDefined(d.Type)) throw new DomainException("Pick a valid type.", nameof(Type));
        if (!Enum.IsDefined(d.Status)) throw new DomainException("Pick a valid status.", nameof(Status));
        Type = d.Type;
        Subject = Rules.Require(d.Subject, nameof(Subject), "Subject", 150);
        Description = Rules.Optional(d.Description, nameof(Description), "Description", Rules.NotesMax);
        When = d.When;
        CustomerId = d.CustomerId;
        LeadId = d.LeadId;
        Status = d.Status;
    }
}
