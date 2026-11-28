using K12.Domain.Content;
using K12.Infrastructure.Persistence;
using K12.Application.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace K12.Api.Tests;

/// <summary>
/// The real API in memory. Forces the in-memory store so a developer's user-secrets connection string
/// is never touched by tests, and points pictures at a temp folder.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string Environment { get; init; } = "Development";

    /// <summary>Who a request with no session cookie acts as in Development. Empty means nobody: a guest.</summary>
    public string DevelopmentUser { get; init; } = "owner@localhost";
    /// <summary>Set to offer Google sign-in. Tokens still need Google to verify them, so tests only check refusals.</summary>
    public string GoogleClientId { get; init; } = "";

    /// <summary>
    /// Replaces Google with a stand-in that accepts tokens shaped "subject|email|authoritative-or-not", so tests can
    /// follow a Google sign-in end to end. The real token checks are Google's library's.
    /// </summary>
    public bool FakeGoogle { get; init; }

    /// <summary>More owner emails, after owner@localhost.</summary>
    public string[] MoreOwners { get; init; } = [];

    /// <summary>Access:ImportKey, the content pipeline's key. Empty means no key is set.</summary>
    public string ImportKey { get; init; } = "";

    /// <summary>A folder to serve as the built web app (wwwroot), or null for none, as in a local run.</summary>
    public string? WebRoot { get; init; }

    public string ImageRoot { get; } = Directory.CreateTempSubdirectory("k12-api-images-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:K12", "");
        builder.UseSetting("Content:ImageRoot", ImageRoot);
        builder.UseSetting("Access:DevelopmentUser", DevelopmentUser);
        builder.UseSetting("Access:Owners:0", "owner@localhost");
        builder.UseSetting("Auth:Google:ClientId", GoogleClientId);
        builder.UseSetting("Access:ImportKey", ImportKey);
        if (WebRoot is not null) builder.UseWebRoot(WebRoot);
        for (var i = 0; i < MoreOwners.Length; i++) builder.UseSetting($"Access:Owners:{i + 1}", MoreOwners[i]);
        if (FakeGoogle)
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IExternalLoginVerifier>();
                services.AddSingleton<IExternalLoginVerifier, StandInGoogle>();
            });
    }

    private sealed class StandInGoogle : IExternalLoginVerifier
    {
        public string Provider => "google";
        public bool IsConfigured => true;
        public string? ClientId => "test.apps.googleusercontent.com";

        public Task<VerifiedLogin?> VerifyAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(token.Split('|') is [var sub, var email, var authoritative]
                ? new VerifiedLogin("google", sub, email, "Pat", authoritative == "authoritative")
                : null);
    }

    public bool UsesInMemoryStore() =>
        Services.GetRequiredService<IQuestionRepository>() is InMemoryQuestionRepository;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(ImageRoot)) Directory.Delete(ImageRoot, recursive: true);
    }
}
