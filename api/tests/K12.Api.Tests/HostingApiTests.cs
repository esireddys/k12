using System.Net;
using System.Net.Http.Json;

namespace K12.Api.Tests;

/// <summary>What a deployed API adds: the pipeline's import key, and serving the built web app from the same origin.</summary>
public class HostingApiTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-import";

    [Theory]
    [InlineData(Key, null, HttpStatusCode.OK)]
    [InlineData(Key, Key, HttpStatusCode.OK)]
    [InlineData(Key, "wrong-key-0123456789abcdef0123456789", HttpStatusCode.Unauthorized)]
    [InlineData(Key, "", HttpStatusCode.Unauthorized)]
    [InlineData("", "", HttpStatusCode.Unauthorized)]
    [InlineData("short", "short", HttpStatusCode.Unauthorized)]
    public async Task Outside_development_importing_needs_the_import_key(string configured, string? sent, HttpStatusCode expected)
    {
        using var production = new ApiFactory { Environment = "Production", DevelopmentUser = "", ImportKey = configured };
        var client = production.CreateClient();
        // null: the right key; anything else is sent as typed, and "" means no header at all.
        var header = sent ?? configured;
        if (header.Length > 0) client.DefaultRequestHeaders.Add("X-Import-Key", header);

        var response = await client.PostAsync("/api/admin/import/publish-released", null);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task The_import_key_opens_only_the_import_endpoints()
    {
        using var production = new ApiFactory { Environment = "Production", DevelopmentUser = "", ImportKey = Key };
        var client = production.CreateClient();
        client.DefaultRequestHeaders.Add("X-Import-Key", Key);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/review/queue")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/family")).StatusCode);
    }

    [Fact]
    public async Task In_development_importing_needs_no_key()
    {
        using var development = new ApiFactory { DevelopmentUser = "" };
        var response = await development.CreateClient().PostAsync("/api/admin/import/publish-released", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task With_a_built_web_app_browser_routes_get_the_app_and_unknown_api_paths_still_404()
    {
        var webRoot = Directory.CreateTempSubdirectory("k12-web-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>app</title>");
            Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
            File.WriteAllText(Path.Combine(webRoot, "assets", "app-abc123.js"), "console.log(1)");
            using var factory = new ApiFactory { Environment = "Production", DevelopmentUser = "", WebRoot = webRoot };
            var client = factory.CreateClient();

            foreach (var route in new[] { "/", "/kids", "/parent/progress" })
            {
                var page = await client.GetAsync(route);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.Contains("<title>app</title>", await page.Content.ReadAsStringAsync());
                Assert.True(page.Headers.CacheControl?.NoCache, $"{route} should be revalidated");
            }

            var asset = await client.GetAsync("/assets/app-abc123.js");
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Contains("immutable", asset.Headers.CacheControl!.ToString());

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/not-a-thing")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            Assert.NotNull(await client.GetFromJsonAsync<object>("/api/auth/options"));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
