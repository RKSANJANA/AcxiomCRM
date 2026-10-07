using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Acxiom.Tests.Integration;

[Collection("app")]
public class WorkflowTests
{
    private readonly CrmApp _app;

    public WorkflowTests(CrmApp app) => _app = app;

    private async Task<HttpClient> BrowserAs(string user)
    {
        var browser = _app.Browser();
        var token = await CrmApp.FormTokenAsync(browser, "/Account/Login");
        await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Input.Email"] = $"{user}@acxiom.test", ["Input.Password"] = CrmApp.Password
        }));
        return browser;
    }

    private static async Task<HttpResponseMessage> PostForm(HttpClient browser, string page, string url, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await CrmApp.FormTokenAsync(browser, page);
        return await browser.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Qualified_lead_converts_into_customer_and_opportunity()
    {
        var api = await _app.ApiAs("divya");
        var n = Random.Shared.Next(100000, 999999);
        var lead = new { name = "Convert " + n, email = $"conv{n}@x.in", phone = $"9100{n}", source = "Referral", status = "Contacted", expectedValue = 300000 };
        var id = (await (await api.PostAsJsonAsync("/api/leads", lead)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        await api.PutAsJsonAsync($"/api/leads/{id}", lead with { status = "Qualified" });

        var browser = await BrowserAs("divya");
        var res = await PostForm(browser, $"/Leads/Convert/{id}", $"/Leads/Convert/{id}", new()
        {
            ["Input.Name"] = "Convert " + n, ["Input.Email"] = $"conv{n}@x.in", ["Input.Phone"] = $"9100{n}",
            ["Input.DealName"] = "Converted deal", ["Input.Amount"] = "300000", ["Input.Probability"] = "25",
            ["Input.CloseDate"] = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Opportunities/Details/", res.Headers.Location!.ToString());

        var after = await api.GetFromJsonAsync<JsonElement>($"/api/leads/{id}");
        Assert.Equal("Converted", after.GetProperty("status").GetString());
        var customers = await api.GetFromJsonAsync<JsonElement>($"/api/customers?q=conv{n}");
        Assert.Equal(1, customers.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Completing_a_follow_up_moves_a_new_lead_to_contacted()
    {
        var api = await _app.ApiAs("divya");
        var n = Random.Shared.Next(1000, 9999);
        var leadId = (await (await api.PostAsJsonAsync("/api/leads", new { name = "Fresh " + n, email = $"fresh{n}@x.in", source = "Website", status = "New" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var fuId = (await (await api.PostAsJsonAsync("/api/followups", new { target = "Lead", targetId = leadId, dueAt = DateTime.Today.AddDays(1).ToString("yyyy-MM-ddT10:00:00"), subject = "Intro call" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var browser = await BrowserAs("divya");
        var res = await PostForm(browser, $"/FollowUps/Details/{fuId}", $"/FollowUps/Details/{fuId}?handler=Complete", new() { ["Done.Outcome"] = "Interested" });
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        Assert.Equal("Completed", (await api.GetFromJsonAsync<JsonElement>($"/api/followups/{fuId}")).GetProperty("status").GetString());
        Assert.Equal("Contacted", (await api.GetFromJsonAsync<JsonElement>($"/api/leads/{leadId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Deactivating_a_user_kills_their_api_token()
    {
        // register a throwaway user, get a token, then have the admin disable them
        var n = Random.Shared.Next(1000, 9999);
        var email = $"temp{n}@acxiom.test";
        var anon = _app.Browser();
        var reg = await PostForm(anon, "/Account/Register", "/Account/Register", new()
        {
            ["Input.FullName"] = "Temp " + n, ["Input.Email"] = email, ["Input.Password"] = CrmApp.Password, ["Input.Confirm"] = CrmApp.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, reg.StatusCode);

        var api = _app.CreateClient();
        var token = (await (await api.PostAsJsonAsync("/api/auth/token", new { email, password = CrmApp.Password })).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        api.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/customers")).StatusCode);

        var admin = await BrowserAs("admin");
        var users = await admin.GetStringAsync($"/Admin/Users/Index?Q=temp{n}");
        var userId = Regex.Match(users, "/Admin/Users/Edit/([0-9a-f-]{36})").Groups[1].Value;
        var save = await PostForm(admin, $"/Admin/Users/Edit/{userId}", $"/Admin/Users/Edit/{userId}", new()
        {
            ["Input.FullName"] = "Temp " + n, ["Input.Email"] = email, ["Input.Role"] = "SalesExecutive", ["Input.IsActive"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/customers")).StatusCode);
    }
}
