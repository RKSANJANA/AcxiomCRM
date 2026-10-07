namespace Acxiom.Application.Common;

public static class CrmClaims
{
    public const string FullName = "crm:name";

    /// <summary>Space separated user ids whose records the user may see (self + team for managers).</summary>
    public const string Owners = "crm:owners";
}
