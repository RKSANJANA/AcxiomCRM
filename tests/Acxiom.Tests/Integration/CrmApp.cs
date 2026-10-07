using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Acxiom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Acxiom.Tests.Integration;

/// <summary>Runs the real app against a throwaway SQLite file seeded with the demo data.</summary>
public class CrmApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test#Pass2026";
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"acxiom-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (not ConfigureAppConfiguration) so Program sees these while it is still registering services
        builder.UseSetting("ConnectionStrings:Crm", $"Data Source={_db}");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Demo:Seed", "true");
        builder.UseSetting("Demo:Password", Password);
        builder.UseSetting("Auth:SignInPerMinute", "1000");
    }

    public async Task InitializeAsync() => await Seeder.RunAsync(Services);

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch (IOException) { }
    }

    public async Task<HttpClient> ApiAs(string user)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/token", new { email = $"{user}@acxiom.test", password = Password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"].ToString());
        return client;
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<string> FormTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<CrmApp> { }
