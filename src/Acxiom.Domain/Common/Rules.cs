namespace Acxiom.Domain.Common;

public static class Rules
{
    public const string PhonePattern = @"^[6-9]\d{9}$";
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$";
    public const string MoneyPattern = @"^\d{1,10}(\.\d{1,2})?$";

    public const decimal MaxMoney = 1_000_000_000m;

    public const int NameMax = 100;
    public const int EmailMax = 150;
    public const int CompanyMax = 150;
    public const int NotesMax = 1000;

    public static class Messages
    {
        public const string Email = "Enter a valid email address.";
        public const string Phone = "Enter a valid phone number.";
        public const string Amount = "Opportunity Amount must be greater than 0.";
        public const string Probability = "Probability must be between 0 and 100.";
        public const string CloseDate = "Expected Close Date cannot be in the past.";
        public const string FollowUpDate = "Follow-up date cannot be earlier than today.";
        public const string ExpectedValue = "Expected value must be between 0 and 1,000,000,000.";
        public const string Money = "Use a plain number with up to 2 decimals.";
    }

    public static string Require(string? value, string field, string label, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) throw new DomainException($"{label} is required.", field);
        if (v.Length > max) throw new DomainException($"{label} cannot be longer than {max} characters.", field);
        return v;
    }

    public static string? Optional(string? value, string field, string label, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new DomainException($"{label} cannot be longer than {max} characters.", field);
        return v;
    }

    public static string Email(string? value, string field = "Email")
    {
        var v = Require(value, field, "Email", EmailMax).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(v, EmailPattern)) throw new DomainException(Messages.Email, field);
        return v;
    }

    public static string? OptionalEmail(string? value, string field = "Email") =>
        string.IsNullOrWhiteSpace(value) ? null : Email(value, field);

    public static string Phone(string? value, string field = "Phone")
    {
        var v = value?.Trim().Replace(" ", "") ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(v, PhonePattern)) throw new DomainException(Messages.Phone, field);
        return v;
    }

    public static string? OptionalPhone(string? value, string field = "Phone") =>
        string.IsNullOrWhiteSpace(value) ? null : Phone(value, field);
}
