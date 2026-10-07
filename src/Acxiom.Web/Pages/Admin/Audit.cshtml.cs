using Acxiom.Application.Auditing;
using Acxiom.Application.Common;
using Acxiom.Application.Users;
using Acxiom.Domain.Auditing;
using Acxiom.Web.Support;

namespace Acxiom.Web.Pages.Admin;

public class AuditModel : CrmPage
{
    private readonly AuditQuery _audit;
    private readonly IUserAdmin _admin;

    public AuditModel(AuditQuery audit, IUserAdmin admin)
    {
        _audit = audit;
        _admin = admin;
    }

    public AuditFilter Filter { get; private set; } = new();
    public Paged<AuditEntry> Result { get; private set; } = null!;
    public List<UserSummary> Users { get; private set; } = new();

    public static readonly string[] Entities = { "Account", "User", "Team", "Customer", "Lead", "Opportunity", "FollowUp", "Activity" };
    public static readonly string[] Actions = { "Login", "LoginFailed", "Lockout", "Logout", "Register", "PasswordChange", "PasswordReset", "Unlock", "Create", "Update", "Delete", "Convert", "RoleChange", "Activate", "Deactivate" };

    public async Task OnGetAsync(AuditFilter filter)
    {
        filter.Size = 30;
        Filter = filter;
        Result = await _audit.SearchAsync(filter);
        Users = await _admin.ListAsync(null, null, null);
    }
}
