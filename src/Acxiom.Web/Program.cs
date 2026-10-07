using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Acxiom.Application;
using Acxiom.Domain;
using Acxiom.Infrastructure;
using Acxiom.Infrastructure.Identity;
using Acxiom.Infrastructure.Persistence;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config, builder.Environment.ContentRootPath);

// --- identity (cookie for the site) ---
var auth = config.GetSection("Auth");
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = auth.GetValue("MinPasswordLength", 8);
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireDigit = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = auth.GetValue("MaxFailedLogins", 5);
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(auth.GetValue("LockoutMinutes", 15));
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<CrmDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<CrmClaimsFactory>();

// claims (role, team list) are rebuilt every minute; a changed stamp signs the user out
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "acx.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});

// --- JWT bearer (for the REST API) ---
builder.Services.Configure<JwtSettings>(config.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokens>();
var jwt = config.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication()
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey(),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // reject tokens of users that were deactivated / had their stamp changed after the token was issued
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await users.FindByIdAsync(ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
                var stamp = ctx.Principal!.FindFirstValue("AspNet.Identity.SecurityStamp");
                if (user is not { IsActive: true } || stamp != user.SecurityStamp) ctx.Fail("Token no longer valid.");
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
    o.AddPolicy("Api", p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", "Admin");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/Register");
    o.Conventions.AllowAnonymousToPage("/Account/Denied");
    o.Conventions.AllowAnonymousToPage("/Status");
    o.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, CrmAdapterProvider>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("signin", ctx => RateLimitPartition.GetSlidingWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = auth.GetValue("SignInPerMinute", 10), Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 4 }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Acxiom CRM API", Version = "v1", Description = "Get a token from POST /api/auth/token, then click Authorize." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

app.UseMiddleware<ErrorMapping>();
app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api"), b => b.UseStatusCodePagesWithReExecute("/Status", "?code={0}"));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapRazorPages();
app.MapControllers();

if (!app.Environment.IsEnvironment("Testing")) await Seeder.RunAsync(app.Services);
app.Run();

public partial class Program { }
