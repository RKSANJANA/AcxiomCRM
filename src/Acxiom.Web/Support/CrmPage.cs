using Acxiom.Application.Common;
using Acxiom.Domain.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acxiom.Web.Support;

public abstract class CrmPage : PageModel
{
    protected void Toast(string message) => TempData["toast"] = message;

    /// <summary>
    /// Runs a command. Business rule / conflict errors are added to ModelState (next to the
    /// field they belong to, inside "Input") and false is returned so the page can redisplay.
    /// </summary>
    protected async Task<bool> TryAsync(Func<Task> action, string prefix = "Input")
    {
        if (!ModelState.IsValid) return false;
        try
        {
            await action();
            return true;
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(Key(ex.Field, prefix), ex.Message);
        }
        catch (ConflictException ex)
        {
            ModelState.AddModelError(Key(ex.Field, prefix), ex.Message);
        }
        return false;
    }

    private static string Key(string? field, string prefix) =>
        field == null ? "" : string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
}
