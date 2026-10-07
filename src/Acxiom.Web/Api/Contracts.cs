using System.ComponentModel.DataAnnotations;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Web.Support;

namespace Acxiom.Web.Api;

// Request bodies for the REST API. Responses reuse the application's read models
// (CustomerView, LeadView, ...) - never the EF entities.

public record TokenRequest(
    [Required, RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)] string Email,
    [Required] string Password);

public record TokenResponse(string Token, DateTime ExpiresUtc, string Name, string Role);

public class CustomerRequest
{
    [Required, StringLength(Rules.NameMax)] public string Name { get; set; } = "";
    [Required, StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)] public string Email { get; set; } = "";
    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.Messages.Phone)] public string Phone { get; set; } = "";
    [StringLength(Rules.CompanyMax)] public string? Company { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(60)] public string? City { get; set; }
    [StringLength(60)] public string? State { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
    public string? OwnerId { get; set; }

    public CustomerDetails ToDetails() => new(Name, Email, Phone, Company, Address, City, State, Status, Notes);
}

public class LeadRequest
{
    [Required, StringLength(Rules.NameMax)] public string Name { get; set; } = "";
    [StringLength(Rules.EmailMax), RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.Messages.Email)] public string? Email { get; set; }
    [RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.Messages.Phone)] public string? Phone { get; set; }
    [StringLength(Rules.CompanyMax)] public string? Company { get; set; }
    [Required] public LeadSource? Source { get; set; }
    [Required] public LeadStatus? Status { get; set; }
    public LeadPriority Priority { get; set; } = LeadPriority.Normal;
    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = Rules.Messages.ExpectedValue)] public decimal ExpectedValue { get; set; }
    [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
    public string? OwnerId { get; set; }

    public LeadDetails ToDetails() => new(Name, Email, Phone, Company, Source!.Value, Priority, ExpectedValue, Notes);
}

public class OpportunityRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "customerId is required.")] public int CustomerId { get; set; }
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = Rules.Messages.Amount)] public decimal Amount { get; set; }
    [Range(0, 100, ErrorMessage = Rules.Messages.Probability)] public int Probability { get; set; }
    [Required] public Stage? Stage { get; set; }

    [Required, NotBeforeToday(UnlessField = nameof(Stage), UnlessValues = "Won,Lost", ErrorMessage = Rules.Messages.CloseDate)]
    public DateTime? ExpectedCloseDate { get; set; }

    public LeadSource? Source { get; set; }
    [StringLength(Rules.NotesMax)] public string? Notes { get; set; }
    public string? OwnerId { get; set; }

    public OpportunityDetails ToDetails() => new(Name, CustomerId, Amount, Probability, ExpectedCloseDate!.Value, Source, Notes);
}

public class FollowUpRequest
{
    [Required] public TargetKind? Target { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "targetId is required.")] public int TargetId { get; set; }
    [Required, NotBeforeToday(ErrorMessage = Rules.Messages.FollowUpDate)] public DateTime? DueAt { get; set; }
    [Required, StringLength(150)] public string Subject { get; set; } = "";
    public ContactType Type { get; set; } = ContactType.Call;
    [StringLength(Rules.NotesMax)] public string? Remarks { get; set; }
    public string? OwnerId { get; set; }
}
