using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Acxiom.Tests.Integration;

[Collection("app")]
public class ApiTests
{
    private readonly CrmApp _app;

    public ApiTests(CrmApp app) => _app = app;

    private static string Tomorrow => DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
    private static string Yesterday => DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task No_token_means_401()
    {
        var res = await _app.CreateClient().GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_gets_no_token()
    {
        var res = await _app.CreateClient().PostAsJsonAsync("/api/auth/token", new { email = "ravi@acxiom.test", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Sales_exec_only_sees_own_customers_and_gets_404_for_others()
    {
        var ravi = await _app.ApiAs("ravi");
        var mine = await Json(await ravi.GetAsync("/api/customers?size=100"));
        var me = mine.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("ownerName").GetString()).Distinct().ToList();
        Assert.Equal(new[] { "Ravi Teja" }, me);

        var admin = await _app.ApiAs("admin");
        var all = await Json(await admin.GetAsync("/api/customers?size=100"));
        var other = all.GetProperty("items").EnumerateArray().First(c => c.GetProperty("ownerName").GetString() == "Arjun Das");

        var res = await ravi.GetAsync($"/api/customers/{other.GetProperty("id").GetInt32()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Manager_sees_their_team_but_not_the_other_team()
    {
        var meera = await _app.ApiAs("meera");
        var list = await Json(await meera.GetAsync("/api/customers?size=100"));
        var owners = list.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("ownerName").GetString()).Distinct().ToList();

        Assert.Contains("Ravi Teja", owners);
        Assert.DoesNotContain("Arjun Das", owners);
    }

    [Fact]
    public async Task Opportunity_rules_are_enforced_by_the_api()
    {
        var ravi = await _app.ApiAs("ravi");
        var customerId = (await Json(await ravi.GetAsync("/api/customers?size=1"))).GetProperty("items")[0].GetProperty("id").GetInt32();

        object Deal(decimal amount, int probability, string close) =>
            new { name = "API deal", customerId, amount, probability, stage = "Proposal", expectedCloseDate = close };

        var zero = await ravi.PostAsJsonAsync("/api/opportunities", Deal(0, 50, Tomorrow));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Contains("Opportunity Amount must be greater than 0.", await zero.Content.ReadAsStringAsync());

        var tooLikely = await ravi.PostAsJsonAsync("/api/opportunities", Deal(5000, 101, Tomorrow));
        Assert.Contains("Probability must be between 0 and 100.", await tooLikely.Content.ReadAsStringAsync());

        var past = await ravi.PostAsJsonAsync("/api/opportunities", Deal(5000, 50, Yesterday));
        Assert.Contains("Expected Close Date cannot be in the past.", await past.Content.ReadAsStringAsync());

        var ok = await ravi.PostAsJsonAsync("/api/opportunities", Deal(200000, 30, Tomorrow));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(60000m, (await Json(ok)).GetProperty("weighted").GetDecimal());
    }

    [Fact]
    public async Task Follow_up_in_the_past_is_rejected()
    {
        var ravi = await _app.ApiAs("ravi");
        var customerId = (await Json(await ravi.GetAsync("/api/customers?size=1"))).GetProperty("items")[0].GetProperty("id").GetInt32();

        var res = await ravi.PostAsJsonAsync("/api/followups", new { target = "Customer", targetId = customerId, dueAt = Yesterday + "T10:00:00", subject = "Late call" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Follow-up date cannot be earlier than today.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Customer_email_and_phone_must_be_unique_across_all_teams()
    {
        var ravi = await _app.ApiAs("ravi");
        var arjun = await _app.ApiAs("arjun");
        var suffix = Random.Shared.Next(1000, 9999);

        var first = await ravi.PostAsJsonAsync("/api/customers", new { name = "Uniq " + suffix, email = $"uniq{suffix}@x.in", phone = $"97000{suffix}0" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // arjun can't see ravi's customer but still can't reuse the email
        var clash = await arjun.PostAsJsonAsync("/api/customers", new { name = "Other " + suffix, email = $"uniq{suffix}@x.in", phone = $"96000{suffix}0" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task Bad_input_is_a_400_with_field_errors()
    {
        var ravi = await _app.ApiAs("ravi");
        var res = await ravi.PostAsJsonAsync("/api/customers", new { name = "", email = "nope", phone = "123" });
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Enter a valid email address.", body);
        Assert.Contains("Enter a valid phone number.", body);
    }

    [Fact]
    public async Task Sales_exec_cannot_delete_but_admin_can()
    {
        var ravi = await _app.ApiAs("ravi");
        var created = await Json(await ravi.PostAsJsonAsync("/api/customers", new { name = "Temp", email = $"temp{Guid.NewGuid():N}@x.in", phone = $"9{Random.Shared.Next(100000000, 999999999)}" }));
        var id = created.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Forbidden, (await ravi.DeleteAsync($"/api/customers/{id}")).StatusCode);

        var admin = await _app.ApiAs("admin");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/customers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/customers/{id}")).StatusCode);
    }

    [Fact]
    public async Task Lead_status_must_follow_the_workflow()
    {
        var ravi = await _app.ApiAs("ravi");
        var lead = new { name = "Flow test", email = "flow@x.in", source = "Website", status = "New", expectedValue = 1000 };
        var created = await Json(await ravi.PostAsJsonAsync("/api/leads", lead));
        var id = created.GetProperty("id").GetInt32();

        var skip = await ravi.PutAsJsonAsync($"/api/leads/{id}", lead with { status = "Qualified" });
        Assert.Equal(HttpStatusCode.BadRequest, skip.StatusCode);

        var next = await ravi.PutAsJsonAsync($"/api/leads/{id}", lead with { status = "Contacted" });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Pipeline_report_is_available()
    {
        var meera = await _app.ApiAs("meera");
        var res = await meera.GetAsync("/api/reports/pipeline");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(5, (await Json(res)).GetProperty("byStage").GetArrayLength());
    }
}
