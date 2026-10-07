using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;

namespace Acxiom.Tests.Domain;

public class OpportunityRules
{
    private static readonly DateTime Today = new(2026, 10, 7);

    private static OpportunityDetails Deal(decimal amount = 1000, int probability = 50, int closeInDays = 10) =>
        new("Deal", 1, amount, probability, Today.AddDays(closeInDays), null, null);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Amount_must_be_positive(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => Opportunity.Open(Deal(amount: amount), Stage.Qualification, "u1", Today));
        Assert.Equal("Opportunity Amount must be greater than 0.", ex.Message);
        Assert.Equal("Amount", ex.Field);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Probability_must_be_0_to_100(int probability)
    {
        var ex = Assert.Throws<DomainException>(() => Opportunity.Open(Deal(probability: probability), Stage.Qualification, "u1", Today));
        Assert.Equal("Probability must be between 0 and 100.", ex.Message);
    }

    [Fact]
    public void Close_date_cannot_be_in_the_past_for_an_open_deal()
    {
        var ex = Assert.Throws<DomainException>(() => Opportunity.Open(Deal(closeInDays: -1), Stage.Qualification, "u1", Today));
        Assert.Equal("Expected Close Date cannot be in the past.", ex.Message);
    }

    [Fact]
    public void Closing_a_deal_ignores_the_close_date_rule_and_locks_it()
    {
        var deal = Opportunity.Open(Deal(), Stage.Proposal, "u1", Today);
        deal.Edit(Deal(closeInDays: -30), Stage.Won, Today, Today);

        Assert.Equal(OpportunityStatus.Won, deal.Status);
        Assert.Equal(100, deal.Probability);
        Assert.Throws<DomainException>(() => deal.Edit(Deal(), Stage.Proposal, Today, Today));
    }

    [Fact]
    public void Cannot_open_a_deal_that_is_already_won() =>
        Assert.Throws<DomainException>(() => Opportunity.Open(Deal(), Stage.Won, "u1", Today));

    [Fact]
    public void Weighted_value_is_amount_times_probability()
    {
        var deal = Opportunity.Open(Deal(amount: 250000, probability: 40), Stage.Proposal, "u1", Today);
        Assert.Equal(100000m, deal.Weighted);
    }
}

public class LeadRules
{
    private static Lead NewLead(LeadStatus start = LeadStatus.New) =>
        Lead.Create("LEAD-1", new LeadDetails("Asha", "asha@example.in", null, null, LeadSource.Website, LeadPriority.Normal, 1000, null), start, "u1");

    [Fact]
    public void Follows_the_workflow()
    {
        var lead = NewLead();
        lead.MoveTo(LeadStatus.Contacted);
        lead.MoveTo(LeadStatus.Qualified);
        Assert.True(lead.CanConvert);
    }

    [Fact]
    public void Cannot_skip_straight_to_qualified() =>
        Assert.Throws<DomainException>(() => NewLead().MoveTo(LeadStatus.Qualified));

    [Fact]
    public void Converted_is_only_reachable_through_conversion() =>
        Assert.Throws<DomainException>(() => NewLead(LeadStatus.Contacted).MoveTo(LeadStatus.Converted));

    [Fact]
    public void Lost_leads_are_final()
    {
        var lead = NewLead();
        lead.MoveTo(LeadStatus.Lost);
        Assert.True(lead.IsClosed);
        Assert.Throws<DomainException>(() => lead.MoveTo(LeadStatus.Contacted));
    }

    [Fact]
    public void Needs_an_email_or_a_phone() =>
        Assert.Throws<DomainException>(() => Lead.Create("L", new LeadDetails("X", null, null, null, LeadSource.Other, LeadPriority.Low, 0, null), LeadStatus.New, "u1"));

    [Fact]
    public void Cannot_start_as_converted() =>
        Assert.Throws<DomainException>(() => NewLead(LeadStatus.Converted));
}

public class FollowUpRules
{
    private static readonly DateTime Today = new(2026, 10, 7);

    [Fact]
    public void Cannot_be_scheduled_before_today()
    {
        var ex = Assert.Throws<DomainException>(() =>
            FollowUp.Schedule(TargetKind.Customer, 1, Today.AddDays(-1), "Call", ContactType.Call, null, "u1", Today));
        Assert.Equal("Follow-up date cannot be earlier than today.", ex.Message);
    }

    [Fact]
    public void Later_today_is_fine()
    {
        var f = FollowUp.Schedule(TargetKind.Customer, 1, Today.AddHours(17), "Call", ContactType.Call, null, "u1", Today);
        Assert.Equal(FollowUpStatus.Planned, f.Status);
    }

    [Fact]
    public void Completed_follow_ups_cannot_be_rescheduled()
    {
        var f = FollowUp.Schedule(TargetKind.Lead, 3, Today.AddDays(1), "Demo", ContactType.Meeting, null, "u1", Today);
        f.Complete("went well", Today);
        Assert.Throws<DomainException>(() => f.Reschedule(Today.AddDays(3), Today));
    }

    [Fact]
    public void Missed_follow_ups_can_be_rescheduled_back_to_planned()
    {
        var f = FollowUp.Schedule(TargetKind.Lead, 3, Today, "Demo", ContactType.Meeting, null, "u1", Today);
        f.MarkMissed();
        f.Reschedule(Today.AddDays(2), Today);
        Assert.Equal(FollowUpStatus.Planned, f.Status);
    }

    [Fact]
    public void Overdue_means_planned_and_in_the_past()
    {
        var f = FollowUp.Schedule(TargetKind.Lead, 3, Today, "Demo", ContactType.Meeting, null, "u1", Today);
        Assert.True(f.IsOverdue(Today.AddDays(1)));
        Assert.False(f.IsOverdue(Today));
    }
}

public class CustomerRules
{
    private static CustomerDetails Details(string email = "a@b.in", string phone = "9876543210", string name = "Ann") =>
        new(name, email, phone, null, null, null, null, CustomerStatus.Active, null);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public void Rejects_bad_email(string email) =>
        Assert.Equal("Enter a valid email address.", Assert.Throws<DomainException>(() => Customer.Create("C1", Details(email: email), "u1")).Message);

    [Theory]
    [InlineData("12345")]
    [InlineData("5876543210")]
    [InlineData("98765432101")]
    public void Rejects_bad_phone(string phone) =>
        Assert.Equal("Enter a valid phone number.", Assert.Throws<DomainException>(() => Customer.Create("C1", Details(phone: phone), "u1")).Message);

    [Fact]
    public void Name_is_required() =>
        Assert.Throws<DomainException>(() => Customer.Create("C1", Details(name: "  "), "u1"));

    [Fact]
    public void Email_is_stored_lower_case() =>
        Assert.Equal("ann@shop.in", Customer.Create("C1", Details(email: "Ann@Shop.IN"), "u1").Email);
}
