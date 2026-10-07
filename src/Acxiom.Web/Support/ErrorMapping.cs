using Acxiom.Application.Common;
using Acxiom.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Support;

/// <summary>
/// Turns the application's exceptions into responses. API calls get ProblemDetails JSON;
/// pages get a status code, which the status code page then renders.
/// Anything unexpected is logged and shown as a generic error - never a stack trace.
/// </summary>
public class ErrorMapping
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorMapping> _log;

    public ErrorMapping(RequestDelegate next, ILogger<ErrorMapping> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var (status, title, field) = ex switch
            {
                NotFoundException => (404, ex.Message, null),
                ForbiddenException => (403, ex.Message, null),
                ConflictException c => (409, c.Message, c.Field),
                DomainException d => (400, d.Message, d.Field),
                _ => (500, "Something went wrong on our side.", (string?)null)
            };
            if (status == 500) _log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);

            ctx.Response.Clear();
            ctx.Response.StatusCode = status;
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                var problem = new ValidationProblemDetails { Status = status, Title = title };
                if (field != null) problem.Errors[field] = new[] { title };
                await ctx.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
            }
        }
    }
}
