using Acxiom.Domain.Common;

namespace Acxiom.Domain.Leads;

public enum LeadStatus { New, Contacted, Qualified, Unqualified, Converted, Lost }
public enum LeadSource { Website, Referral, SocialMedia, Email, Phone, Event, Partner, Other }
public enum LeadPriority { Low, Normal, High }

public record LeadDetails(
    string? Name, string? Email, string? Phone, string? Company,
    LeadSource Source, LeadPriority Priority, decimal ExpectedValue, string? Notes);

public class Lead : OwnedEntity
{
    // where a lead is allowed to go next. Converted is only reachable through Convert().
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Next = new()
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>(),
        [LeadStatus.Lost] = Array.Empty<LeadStatus>(),
    };

    private Lead() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Company { get; private set; }
    public LeadSource Source { get; private set; }
    public LeadStatus Status { get; private set; }
    public LeadPriority Priority { get; private set; }
    public decimal ExpectedValue { get; private set; }
    public string? Notes { get; private set; }

    public DateTime? ConvertedAt { get; private set; }
    public int? CustomerId { get; private set; }
    public int? OpportunityId { get; private set; }

    public bool IsClosed => Next[Status].Length == 0;
    public bool CanConvert => Status == LeadStatus.Qualified;

    public static Lead Create(string code, LeadDetails details, LeadStatus startStatus, string ownerId)
    {
        if (startStatus is not (LeadStatus.New or LeadStatus.Contacted))
            throw new DomainException("A new lead starts as New or Contacted.", nameof(Status));

        var lead = new Lead { Code = code, Status = startStatus };
        lead.Update(details);
        lead.AssignTo(ownerId);
        return lead;
    }

    public IReadOnlyList<LeadStatus> AllowedStatuses() => Next[Status];

    public void Update(LeadDetails d)
    {
        if (IsClosed) throw new DomainException($"This lead is {Status} and can't be changed.");

        Name = Rules.Require(d.Name, nameof(Name), "Lead Name", Rules.NameMax);
        Email = Rules.OptionalEmail(d.Email);
        Phone = Rules.OptionalPhone(d.Phone);
        if (Email == null && Phone == null)
            throw new DomainException("Give at least an email or a phone number.", nameof(Email));
        Company = Rules.Optional(d.Company, nameof(Company), "Company", Rules.CompanyMax);
        Notes = Rules.Optional(d.Notes, nameof(Notes), "Notes", Rules.NotesMax);

        if (!Enum.IsDefined(d.Source)) throw new DomainException("Pick a valid source.", nameof(Source));
        if (!Enum.IsDefined(d.Priority)) throw new DomainException("Pick a valid priority.", nameof(Priority));
        if (d.ExpectedValue < 0 || d.ExpectedValue > Rules.MaxMoney)
            throw new DomainException(Rules.Messages.ExpectedValue, nameof(ExpectedValue));

        Source = d.Source;
        Priority = d.Priority;
        ExpectedValue = decimal.Round(d.ExpectedValue, 2);
    }

    public void MoveTo(LeadStatus status)
    {
        if (status == Status) return;
        if (!Enum.IsDefined(status)) throw new DomainException("Unknown lead status.", nameof(Status));
        if (status == LeadStatus.Converted)
            throw new DomainException("Use Convert to turn a qualified lead into a customer.", nameof(Status));
        if (!Next[Status].Contains(status))
            throw new DomainException($"A lead can't go from {Status} to {status}.", nameof(Status));
        Status = status;
    }

    public void MarkConverted(int customerId, int opportunityId, DateTime when)
    {
        if (!CanConvert) throw new DomainException("Only qualified leads can be converted.");
        Status = LeadStatus.Converted;
        CustomerId = customerId;
        OpportunityId = opportunityId;
        ConvertedAt = when;
    }
}
