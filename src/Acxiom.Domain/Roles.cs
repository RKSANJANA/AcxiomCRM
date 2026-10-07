namespace Acxiom.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Manager, SalesExecutive };

    public static string Label(string? role) => role == SalesExecutive ? "Sales Executive" : role ?? "";
}
