using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Acxiom.Web.Support;

/// <summary>&lt;th sort="name"&gt;Name&lt;/th&gt; - clickable header that toggles "name" / "-name" in the query string.</summary>
[HtmlTargetElement("th", Attributes = "sort")]
public class SortHeaderTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext View { get; set; } = null!;
    [HtmlAttributeName("sort")] public string Key { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var request = View.HttpContext.Request;
        var current = request.Query["Sort"].ToString();
        var next = current == Key ? "-" + Key : Key;
        var arrow = current == Key ? " ▲" : current == "-" + Key ? " ▼" : "";

        var label = (await output.GetChildContentAsync()).GetContent();
        var url = Links.With(request, ("Sort", next), ("Page", null));
        output.Content.SetHtmlContent($"<a href=\"{System.Net.WebUtility.HtmlEncode(url)}\" class=\"link-dark text-decoration-none\">{label}{arrow}</a>");
    }
}

/// <summary>&lt;status-pill value="@x.Status" /&gt;</summary>
[HtmlTargetElement("status-pill", TagStructure = TagStructure.WithoutEndTag)]
public class StatusPillTagHelper : TagHelper
{
    public object? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var text = Value?.ToString() ?? "";
        var tone = text switch
        {
            "Active" or "Won" or "Done" or "Completed" or "Converted" or "OK" => "success",
            "Lost" or "Missed" or "Overdue" or "Failed" => "danger",
            "Qualified" or "Negotiation" or "High" => "warning",
            "Contacted" or "Proposal" or "Planned" or "Open" => "primary",
            "New" or "Prospect" or "Qualification" => "info",
            _ => "secondary"
        };
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"badge bg-{tone}-subtle text-{tone}-emphasis border border-{tone}-subtle");
        output.Content.SetContent(Spaced(text));
    }

    // "SocialMedia" -> "Social Media"
    public static string Spaced(string s) => System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");
}

public static class Links
{
    public static string With(HttpRequest request, params (string Key, string? Value)[] changes)
    {
        var q = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in request.Query) q[kv.Key] = kv.Value.ToString();
        foreach (var (k, v) in changes) q[k] = v;
        var qb = new QueryBuilder(q.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
        return request.Path + qb.ToString();
    }
}

public static class Format
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Inr(this decimal value) => value.ToString("C0", India);
    public static string Day(this DateTime value) => value.ToString("d MMM yyyy");
    public static string Day(this DateTime? value) => value?.ToString("d MMM yyyy") ?? "";
    public static string Stamp(this DateTime value) => value.ToString("d MMM yyyy, h:mm tt");
}

public static class Lists
{
    public static List<SelectListItem> Of<T>(T? selected = null, IEnumerable<T>? only = null) where T : struct, Enum =>
        (only ?? Enum.GetValues<T>()).Select(v => new SelectListItem(StatusPillTagHelper.Spaced(v.ToString()), v.ToString(), selected?.Equals(v) == true)).ToList();

    public static List<SelectListItem> Of<T>(T selected, IEnumerable<T>? only = null) where T : struct, Enum => Of((T?)selected, only);

    public static List<SelectListItem> Of(IEnumerable<Acxiom.Application.Customers.Option> items, int? selected = null) =>
        items.Select(i => new SelectListItem(i.Text, i.Id.ToString(), i.Id == selected)).ToList();

    public static List<SelectListItem> Of(IEnumerable<Acxiom.Application.UserOption> users, string? selected = null) =>
        users.Select(u => new SelectListItem(u.Name, u.Id, u.Id == selected)).ToList();
}
