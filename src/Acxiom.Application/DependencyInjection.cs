using Acxiom.Application.Activities;
using Acxiom.Application.Auditing;
using Acxiom.Application.Common;
using Acxiom.Application.Customers;
using Acxiom.Application.Dashboard;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Leads;
using Acxiom.Application.Opportunities;
using Acxiom.Application.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Acxiom.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Owners>();

        services.AddScoped<CustomerHandler>();
        services.AddScoped<LeadHandler>();
        services.AddScoped<OpportunityHandler>();
        services.AddScoped<FollowUpHandler>();
        services.AddScoped<ActivityHandler>();
        services.AddScoped<DashboardHandler>();
        services.AddScoped<AuditQuery>();

        services.AddScoped<IReport, CustomerReport>();
        services.AddScoped<IReport, LeadReport>();
        services.AddScoped<IReport, FollowUpReport>();
        services.AddScoped<IReport, OpportunityReport>();
        services.AddScoped<IReport, PipelineReport>();
        services.AddScoped<IReport, ConversionReport>();
        services.AddScoped<IReport, UserActivityReport>();
        services.AddScoped<IReport, AuditReport>();
        return services;
    }
}
