using System.Net;

namespace Acxiom.Tests.Integration;

[Collection("app")]
public class SiteTests
{
    private readonly CrmApp _app;

    public SiteTests(CrmApp app) => _app = app;

    private async Task<HttpResponseMessage> LogIn(HttpClient browser, string email, string password)
    {
        var token = await CrmApp.FormTokenAsync(browser, "/Account/Login");
        return await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = password
        }));
    }

    [Fact]
    public async Task Pages_need_a_login()
    {
        var res = await _app.Browser().GetAsync("/Customers");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Account/Login", res.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Valid_login_lands_on_the_dashboard()
    {
        var browser = _app.Browser();
        var res = await LogIn(browser, "divya@acxiom.test", CrmApp.Password);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        var dashboard = await browser.GetStringAsync("/");
        Assert.Contains("Total customers", dashboard);
        Assert.Contains("leadChart", dashboard);
    }

    [Fact]
    public async Task Server_rejects_invalid_customer_even_without_browser_validation()
    {
        var browser = _app.Browser();
        await LogIn(browser, "divya@acxiom.test", CrmApp.Password);
        var token = await CrmApp.FormTokenAsync(browser, "/Customers/Edit");

        var res = await browser.PostAsync("/Customers/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = "Bad",
            ["Input.Email"] = "bad-email",
            ["Input.Phone"] = "123",
            ["Input.Status"] = "Active"
        }));
        var html = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // re-rendered form, not a redirect
        Assert.Contains("Enter a valid email address.", html);
        Assert.Contains("Enter a valid phone number.", html);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var browser = _app.Browser();
        await LogIn(browser, "divya@acxiom.test", CrmApp.Password);
        var res = await browser.PostAsync("/Customers/Edit", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Name"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Sales_exec_cannot_open_admin_pages()
    {
        var browser = _app.Browser();
        await LogIn(browser, "divya@acxiom.test", CrmApp.Password);
        var res = await browser.GetAsync("/Admin/Users/Index");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Account/Denied", res.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_can_open_admin_pages()
    {
        var browser = _app.Browser();
        await LogIn(browser, "admin@acxiom.test", CrmApp.Password);
        foreach (var page in new[] { "/Admin/Users/Index", "/Admin/Teams", "/Admin/Roles", "/Admin/Audit" })
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(page)).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        // sameer is only used by this test
        var browser = _app.Browser();
        string html = "";
        for (var i = 0; i < 5; i++)
            html = await (await LogIn(browser, "sameer@acxiom.test", "Wrong#" + i)).Content.ReadAsStringAsync();
        Assert.Contains("locked", html);

        var withRightPassword = await (await LogIn(browser, "sameer@acxiom.test", CrmApp.Password)).Content.ReadAsStringAsync();
        Assert.Contains("locked", withRightPassword);
    }

    [Fact]
    public async Task Changes_end_up_in_the_audit_log()
    {
        var ravi = await _app.ApiAs("ravi");
        await ravi.PostAsync("/api/customers", System.Net.Http.Json.JsonContent.Create(new { name = "Audited", email = $"aud{Guid.NewGuid():N}@x.in", phone = $"9{Random.Shared.Next(100000000, 999999999)}" }));

        var browser = _app.Browser();
        await LogIn(browser, "admin@acxiom.test", CrmApp.Password);
        var html = await browser.GetStringAsync("/Admin/Audit?Entity=Customer&Act=Create");
        Assert.Contains("Ravi Teja", html);
    }
}
