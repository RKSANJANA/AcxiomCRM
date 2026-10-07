using Acxiom.Domain.Common;
using Acxiom.Domain.Leads;

namespace Acxiom.Domain.Sales;

public enum Stage { Qualification, Proposal, Negotiation, Won, Lost }
public enum OpportunityStatus { Open, Won, Lost }

public record OpportunityDetails(
    string? Name, int CustomerId, decimal Amount, int Probability,
    DateTime ExpectedCloseDate, LeadSource? Source, string? Notes);

public class Opportunity : OwnedEntity
{
    private Opportunity() { }

    public string Name { get; private set; } = "";
    public int CustomerId { get; private set; }
    public int? LeadId { get; private set; }
    public decimal Amount { get; private set; }
    public int Probability { get; private set; }
    public DateTime ExpectedCloseDate { get; private set; }
    public Stage Stage { get; private set; }
    public OpportunityStatus Status { get; private set; }
    public LeadSource? Source { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public bool IsOpen => Status == OpportunityStatus.Open;
    public decimal Weighted => decimal.Round(Amount * Probability / 100m, 2);

    public static Opportunity Open(OpportunityDetails details, Stage stage, string ownerId, DateTime today, int? leadId = null)
    {
        if (stage is Stage.Won or Stage.Lost)
            throw new DomainException("A new opportunity has to start in an open stage.", nameof(Stage));

        var o = new Opportunity { Stage = stage, Status = OpportunityStatus.Open, LeadId = leadId };
        o.Apply(details, today);
        o.AssignTo(ownerId);
        return o;
    }

    public void Edit(OpportunityDetails details, Stage stage, DateTime today, DateTime now)
    {
        if (!IsOpen) throw new DomainException($"This opportunity is already {Status}.");

        if (stage is Stage.Won or Stage.Lost)
        {
            // closing: the close date rule only applies to open deals
            Apply(details, today, checkCloseDate: false);
            Close(stage == Stage.Won, now);
        }
        else
        {
            Apply(details, today);
            Stage = stage;
        }
    }

    private void Close(bool won, DateTime now)
    {
        Stage = won ? Stage.Won : Stage.Lost;
        Status = won ? OpportunityStatus.Won : OpportunityStatus.Lost;
        Probability = won ? 100 : 0;
        ClosedAt = now;
    }

    private void Apply(OpportunityDetails d, DateTime today, bool checkCloseDate = true)
    {
        Name = Rules.Require(d.Name, nameof(Name), "Opportunity Name", 150);
        if (d.CustomerId <= 0) throw new DomainException("Customer is required.", nameof(CustomerId));
        if (d.Amount <= 0) throw new DomainException(Rules.Messages.Amount, nameof(Amount));
        if (d.Amount > Rules.MaxMoney) throw new DomainException("Amount is too large.", nameof(Amount));
        if (d.Probability is < 0 or > 100) throw new DomainException(Rules.Messages.Probability, nameof(Probability));
        if (checkCloseDate && d.ExpectedCloseDate.Date < today.Date)
            throw new DomainException(Rules.Messages.CloseDate, nameof(ExpectedCloseDate));

        CustomerId = d.CustomerId;
        Amount = decimal.Round(d.Amount, 2);
        Probability = d.Probability;
        ExpectedCloseDate = d.ExpectedCloseDate.Date;
        Source = d.Source;
        Notes = Rules.Optional(d.Notes, nameof(Notes), "Notes", Rules.NotesMax);
    }
}
