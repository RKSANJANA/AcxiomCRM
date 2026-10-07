using Acxiom.Domain.Common;

namespace Acxiom.Domain.FollowUps;

public enum FollowUpStatus { Planned, Completed, Missed, Cancelled }
public enum ContactType { Call, Meeting, Email, Task }
public enum TargetKind { Customer, Lead, Opportunity }

public class FollowUp : OwnedEntity
{
    private FollowUp() { }

    public int? CustomerId { get; private set; }
    public int? LeadId { get; private set; }
    public int? OpportunityId { get; private set; }

    public DateTime DueAt { get; private set; }
    public string Subject { get; private set; } = "";
    public ContactType Type { get; private set; }
    public string? Remarks { get; private set; }
    public FollowUpStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public TargetKind Target => OpportunityId.HasValue ? TargetKind.Opportunity : LeadId.HasValue ? TargetKind.Lead : TargetKind.Customer;
    public int TargetId => OpportunityId ?? LeadId ?? CustomerId ?? 0;

    public bool IsOverdue(DateTime today) => Status == FollowUpStatus.Planned && DueAt.Date < today.Date;

    public static FollowUp Schedule(TargetKind target, int targetId, DateTime dueAt, string? subject, ContactType type,
        string? remarks, string ownerId, DateTime today)
    {
        if (targetId <= 0) throw new DomainException("Pick what this follow-up is for.", "Target");
        var f = new FollowUp { Status = FollowUpStatus.Planned };
        switch (target)
        {
            case TargetKind.Customer: f.CustomerId = targetId; break;
            case TargetKind.Lead: f.LeadId = targetId; break;
            case TargetKind.Opportunity: f.OpportunityId = targetId; break;
            default: throw new DomainException("Pick what this follow-up is for.", "Target");
        }
        f.Change(dueAt, subject, type, remarks, today);
        f.AssignTo(ownerId);
        return f;
    }

    public void Change(DateTime dueAt, string? subject, ContactType type, string? remarks, DateTime today)
    {
        EnsurePlanned("edited");
        CheckDate(dueAt, today, nameof(DueAt));
        if (!Enum.IsDefined(type)) throw new DomainException("Pick a valid type.", nameof(Type));
        DueAt = dueAt;
        Subject = Rules.Require(subject, nameof(Subject), "Subject", 150);
        Type = type;
        Remarks = Rules.Optional(remarks, nameof(Remarks), "Remarks", Rules.NotesMax);
    }

    public void Reschedule(DateTime newDueAt, DateTime today)
    {
        if (Status is not (FollowUpStatus.Planned or FollowUpStatus.Missed))
            throw new DomainException($"A {Status.ToString().ToLower()} follow-up can't be rescheduled.");
        CheckDate(newDueAt, today, "NewDate");
        DueAt = newDueAt;
        Status = FollowUpStatus.Planned;
    }

    public void Complete(string? outcome, DateTime now)
    {
        EnsurePlanned("completed");
        Status = FollowUpStatus.Completed;
        CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(outcome))
            Remarks = Rules.Optional(outcome, nameof(Remarks), "Outcome", Rules.NotesMax);
    }

    public void MarkMissed()
    {
        EnsurePlanned("marked as missed");
        Status = FollowUpStatus.Missed;
    }

    public void Cancel()
    {
        EnsurePlanned("cancelled");
        Status = FollowUpStatus.Cancelled;
    }

    private void EnsurePlanned(string what)
    {
        if (Status != FollowUpStatus.Planned)
            throw new DomainException($"Only planned follow-ups can be {what}.");
    }

    private static void CheckDate(DateTime when, DateTime today, string field)
    {
        if (when.Date < today.Date) throw new DomainException(Rules.Messages.FollowUpDate, field);
    }
}
