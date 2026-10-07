using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Acxiom.Web.Support;

public class JwtSettings
{
    public string Issuer { get; set; } = "acxiom-crm";
    public string Audience { get; set; } = "acxiom-crm-api";
    public string Key { get; set; } = "";
    public int Minutes { get; set; } = 60;

    public SymmetricSecurityKey SigningKey()
    {
        if (Key.Length < 32) throw new InvalidOperationException("Jwt:Key must be set (at least 32 characters). Use user-secrets or an environment variable.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    }
}

public class JwtTokens
{
    private readonly JwtSettings _settings;

    public JwtTokens(Microsoft.Extensions.Options.IOptions<JwtSettings> settings) => _settings = settings.Value;

    public (string Token, DateTime Expires) Issue(ClaimsPrincipal principal)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.Minutes);
        var token = new JwtSecurityToken(
            _settings.Issuer, _settings.Audience, principal.Claims, DateTime.UtcNow, expires,
            new SigningCredentials(_settings.SigningKey(), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
