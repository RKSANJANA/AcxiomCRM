using Acxiom.Application;
using Acxiom.Infrastructure.Identity;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Acxiom.Web.Api;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly JwtTokens _tokens;
    private readonly IAuditLog _audit;

    public AuthController(SignInManager<AppUser> signIn, JwtTokens tokens, IAuditLog audit)
    {
        _signIn = signIn;
        _tokens = tokens;
        _audit = audit;
    }

    /// <summary>Exchange email + password for a bearer token (valid for an hour by default).</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("signin")]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Token(TokenRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _signIn.UserManager.FindByEmailAsync(email);
        if (user == null)
        {
            await _audit.WriteAsync("LoginFailed", "Account", note: "api: unknown email", succeeded: false, userName: email);
            return Problem("Email or password is incorrect.", statusCode: 401);
        }

        var check = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut || !check.Succeeded || !user.IsActive)
        {
            var why = check.IsLockedOut ? "locked out" : !check.Succeeded ? "wrong password" : "account disabled";
            await _audit.WriteAsync(check.IsLockedOut ? "Lockout" : "LoginFailed", "Account", user.Id, "api: " + why, false, user.Id, email);
            return Problem(check.IsLockedOut ? "Account is locked. Try again later." : "Email or password is incorrect.", statusCode: 401);
        }

        // same claims as the website session, including the team scope
        var principal = await _signIn.CreateUserPrincipalAsync(user);
        var (token, expires) = _tokens.Issue(principal);
        var role = (await _signIn.UserManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
        await _audit.WriteAsync("Login", "Account", user.Id, "api token", userId: user.Id, userName: email);
        return Ok(new TokenResponse(token, expires, user.FullName, role));
    }
}
