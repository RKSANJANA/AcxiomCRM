using System.Security.Claims;
using Acxiom.Application;
using Acxiom.Application.Common;
using Acxiom.Domain;

namespace Acxiom.Web.Support;

/// <summary>Reads the signed-in user from the cookie or JWT claims.</summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public HttpCurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public string? Id => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Name => Principal?.FindFirstValue(CrmClaims.FullName) ?? Principal?.Identity?.Name;
    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public bool IsSystem => _http.HttpContext == null;
    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) == true;
    public bool IsManager => Principal?.IsInRole(Roles.Manager) == true;

    public IReadOnlyList<string> OwnerIds
    {
        get
        {
            if (!IsAuthenticated) return Array.Empty<string>();
            var ids = Principal!.FindFirstValue(CrmClaims.Owners)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return ids is { Length: > 0 } ? ids : Id != null ? new[] { Id } : Array.Empty<string>();
        }
    }
}
