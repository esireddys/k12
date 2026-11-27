using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace K12.Api.Tests;

/// <summary>Sign-in, the session cookie and the family endpoints, end to end. Requests here start as guests.</summary>
public sealed class AccountApiTests : IDisposable
{
    private readonly ApiFactory _factory = new() { DevelopmentUser = "" };

    public void Dispose() => _factory.Dispose();

    private static async Task<JsonObject> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task A_guest_is_not_signed_in_and_cannot_reach_the_family()
    {
        var client = _factory.CreateClient();
        var me = await client.GetFromJsonAsync<JsonObject>("/api/me");
        Assert.Equal((false, false, false), ((bool)me!["isSignedIn"]!, (bool)me["hasFamily"]!, (bool)me["isOwner"]!));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family")).StatusCode);
    }

    [Fact]
    public async Task The_sign_in_screen_learns_which_options_this_server_has()
    {
        var options = await _factory.CreateClient().GetFromJsonAsync<JsonObject>("/api/auth/options");
        Assert.Empty(options!["providers"]!.AsArray());
        Assert.True((bool)options["developerSignIn"]!);

        using var withGoogle = new ApiFactory { DevelopmentUser = "", GoogleClientId = "abc.apps.googleusercontent.com" };
        var google = await withGoogle.CreateClient().GetFromJsonAsync<JsonObject>("/api/auth/options");
        var provider = Assert.Single(google!["providers"]!.AsArray());
        Assert.Equal(("google", "abc.apps.googleusercontent.com"), ((string)provider!["provider"]!, (string)provider["clientId"]!));
    }

    [Fact]
    public async Task Google_sign_in_refuses_a_token_it_cannot_verify()
    {
        using var withGoogle = new ApiFactory { DevelopmentUser = "", GoogleClientId = "abc.apps.googleusercontent.com" };
        var response = await withGoogle.CreateClient().PostAsJsonAsync("/api/auth/google", new { token = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(response.Headers, h => h.Key == "Set-Cookie");
    }

    [Fact]
    public async Task A_signed_in_parent_manages_kids_and_signing_out_ends_the_session()
    {
        var client = _factory.CreateClient();
        var account = await Json(await client.PostAsJsonAsync("/api/auth/developer", new { email = "Pat@Example.org", name = "Pat" }));
        Assert.Equal("pat@example.org", (string)account["email"]!);

        var me = await client.GetFromJsonAsync<JsonObject>("/api/me");
        Assert.Equal(("Pat", true, true, false), ((string)me!["name"]!, (bool)me["isSignedIn"]!, (bool)me["hasFamily"]!, (bool)me["isOwner"]!));

        var kid = await Json(await client.PostAsJsonAsync("/api/family/kids", new { name = "Maya", avatar = "Orange" }));
        var kidId = (string)kid["id"]!;
        await Json(await client.PutAsJsonAsync($"/api/family/kids/{kidId}", new { name = "Maya R", avatar = "Teal" }));

        var family = await client.GetFromJsonAsync<JsonObject>("/api/family");
        var only = Assert.Single(family!["kids"]!.AsArray());
        Assert.Equal(("Maya R", "Teal"), ((string)only!["name"]!, (string)only["avatar"]!));

        var bad = await client.PostAsJsonAsync("/api/family/kids", new { name = "maya@example.org", avatar = "Blue" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        Assert.Empty((await Json(await client.DeleteAsync($"/api/family/kids/{kidId}")))["kids"]!.AsArray());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/sign-out", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/family")).StatusCode);
    }

    [Fact]
    public async Task Each_parent_sees_only_their_own_family()
    {
        var pat = _factory.CreateClient();
        var sam = _factory.CreateClient();
        await Json(await pat.PostAsJsonAsync("/api/auth/developer", new { email = "pat@example.org" }));
        await Json(await sam.PostAsJsonAsync("/api/auth/developer", new { email = "sam@example.org" }));
        var patsKid = (string)(await Json(await pat.PostAsJsonAsync("/api/family/kids", new { name = "Maya", avatar = "Blue" })))["id"]!;

        Assert.Empty((await sam.GetFromJsonAsync<JsonObject>("/api/family"))!["kids"]!.AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await sam.DeleteAsync($"/api/family/kids/{patsKid}")).StatusCode);
        Assert.Single((await pat.GetFromJsonAsync<JsonObject>("/api/family"))!["kids"]!.AsArray());
    }

    [Fact]
    public async Task An_owner_who_signs_in_gets_the_owner_role_from_configuration()
    {
        var client = _factory.CreateClient();
        await Json(await client.PostAsJsonAsync("/api/auth/developer", new { email = "owner@localhost" }));
        var me = await client.GetFromJsonAsync<JsonObject>("/api/me");
        Assert.True((bool)me!["isOwner"]!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/review/queue")).StatusCode);
    }

    [Fact]
    public async Task Outside_development_there_is_no_developer_sign_in()
    {
        using var production = new ApiFactory { Environment = "Production", DevelopmentUser = "" };
        var client = production.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/developer", new { email = "owner@localhost" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((bool)(await client.GetFromJsonAsync<JsonObject>("/api/auth/options"))!["developerSignIn"]!);
        // Nor can a header stand in for a sign-in.
        client.DefaultRequestHeaders.Add("X-Dev-User", "owner@localhost");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/review/queue")).StatusCode);
    }
}
