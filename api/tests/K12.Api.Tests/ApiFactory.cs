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
    public string ImageRoot { get; } = Directory.CreateTempSubdirectory("k12-api-images-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:K12", "");
        builder.UseSetting("Content:ImageRoot", ImageRoot);
        builder.UseSetting("Access:DevelopmentUser", "owner@localhost");
        builder.UseSetting("Access:Owners:0", "owner@localhost");
    }

    public bool UsesInMemoryStore() =>
        Services.GetRequiredService<IQuestionRepository>() is InMemoryQuestionRepository;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(ImageRoot)) Directory.Delete(ImageRoot, recursive: true);
    }
}
