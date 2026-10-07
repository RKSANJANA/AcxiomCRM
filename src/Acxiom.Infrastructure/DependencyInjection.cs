using Acxiom.Application;
using Acxiom.Application.Users;
using Acxiom.Infrastructure.Identity;
using Acxiom.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acxiom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, string contentRoot)
    {
        var cs = new SqliteConnectionStringBuilder(config.GetConnectionString("Crm")
            ?? throw new InvalidOperationException("Missing connection string 'Crm'."));
        if (!Path.IsPathRooted(cs.DataSource) && cs.DataSource != ":memory:")
        {
            cs.DataSource = Path.Combine(contentRoot, cs.DataSource);
            Directory.CreateDirectory(Path.GetDirectoryName(cs.DataSource)!);
        }

        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<CrmDbContext>((sp, o) => o
            .UseSqlite(cs.ToString())
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserAdmin, UserAdmin>();
        return services;
    }
}
