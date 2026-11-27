using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace K12.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the model and reach the database without starting the API. It reads
/// <c>ConnectionStrings:K12</c> the way the API does: the API's appsettings files, then its user secrets
/// (in Development), then environment variables such as <c>ConnectionStrings__K12</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<K12DbContext>
{
    /// <summary>The K12.Api project's UserSecretsId; a test keeps it in step with K12.Api.csproj.</summary>
    internal const string ApiUserSecretsId = "09c1dca9-725c-4c66-b425-15ef1003b849";

    /// <summary>Used only when nothing is configured, so `dotnet ef migrations add` still works without a database.</summary>
    internal const string Fallback = "Server=localhost;Database=K12;Trusted_Connection=True;TrustServerCertificate=True";

    public K12DbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<K12DbContext>()
            .UseSqlServer(ConnectionString(Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(e => (string)e.Key, e => (string?)e.Value)))
            .Options);

    internal static string ConnectionString(string currentDirectory, IReadOnlyDictionary<string, string?> environment, string? userSecretsId = ApiUserSecretsId)
    {
        var name = environment.GetValueOrDefault("ASPNETCORE_ENVIRONMENT")
            ?? environment.GetValueOrDefault("DOTNET_ENVIRONMENT")
            ?? "Development";
        var builder = new ConfigurationBuilder();
        if (FindApiFolder(currentDirectory) is { } api)
            builder.SetBasePath(api)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{name}.json", optional: true);
        if (userSecretsId is not null && name == "Development")
            builder.AddUserSecrets(userSecretsId);
        builder.AddInMemoryCollection(environment
            .Where(e => e.Key.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<string, string?>(e.Key.Replace("__", ":"), e.Value)));

        var value = builder.Build().GetConnectionString("K12");
        return string.IsNullOrWhiteSpace(value) ? Fallback : value;
    }

    /// <summary>The K12.Api folder: `dotnet ef` runs in the startup project's folder, but a plain run may start higher up.</summary>
    private static string? FindApiFolder(string start) =>
        new[] { start, Path.Combine(start, "src", "K12.Api"), Path.Combine(start, "K12.Api"), Path.Combine(start, "..", "K12.Api") }
            .Select(Path.GetFullPath)
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "K12.Api.csproj")));
}
