using K12.Domain.Content;
using K12.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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

    public string ImageRoot { get; } = Directory.CreateTempSubdirectory("k12-api-images-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:K12", "");
        builder.UseSetting("Content:ImageRoot", ImageRoot);
        builder.UseSetting("Access:DevelopmentUser", DevelopmentUser);
        builder.UseSetting("Access:Owners:0", "owner@localhost");
        builder.UseSetting("Auth:Google:ClientId", GoogleClientId);
    }

    public bool UsesInMemoryStore() =>
        Services.GetRequiredService<IQuestionRepository>() is InMemoryQuestionRepository;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(ImageRoot)) Directory.Delete(ImageRoot, recursive: true);
    }
}
