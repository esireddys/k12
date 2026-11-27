using System.Xml.Linq;
using K12.Infrastructure.Persistence;

namespace K12.Infrastructure.Tests;

public sealed class DesignTimeDbContextFactoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("k12-ef-").FullName;
    private string Api => Path.Combine(_root, "src", "K12.Api");

    public DesignTimeDbContextFactoryTests()
    {
        Directory.CreateDirectory(Api);
        File.WriteAllText(Path.Combine(Api, "K12.Api.csproj"), "<Project />");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Dictionary<string, string?> Env(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => (string?)p.Value);

    private static string Settings(string server) =>
        $$"""{ "ConnectionStrings": { "K12": "Server={{server.Replace("\\", "\\\\")}};Database=K12" } }""";

    [Theory]
    [InlineData("api")]
    [InlineData("root")]
    public void Reads_the_api_appsettings_from_the_api_folder_or_the_repo_folder(string from)
    {
        File.WriteAllText(Path.Combine(Api, "appsettings.Development.json"), Settings(@"localhost\SQLEXPRESS"));

        var value = DesignTimeDbContextFactory.ConnectionString(from == "api" ? Api : _root, Env(), userSecretsId: null);

        Assert.Equal(@"Server=localhost\SQLEXPRESS;Database=K12", value);
    }

    [Fact]
    public void An_environment_variable_wins_over_appsettings()
    {
        File.WriteAllText(Path.Combine(Api, "appsettings.json"), Settings("from-file"));

        var value = DesignTimeDbContextFactory.ConnectionString(Api, Env(("ConnectionStrings__K12", "Server=from-env")), userSecretsId: null);

        Assert.Equal("Server=from-env", value);
    }

    [Fact]
    public void Falls_back_to_the_default_instance_when_nothing_is_set() =>
        Assert.Equal(DesignTimeDbContextFactory.Fallback, DesignTimeDbContextFactory.ConnectionString(_root, Env(), userSecretsId: null));

    [Fact]
    public void Uses_the_same_user_secrets_as_the_api()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "src", "K12.Api", "K12.Api.csproj"))) dir = Path.GetDirectoryName(dir)!;
        var id = XDocument.Load(Path.Combine(dir, "src", "K12.Api", "K12.Api.csproj")).Descendants("UserSecretsId").Single().Value;

        Assert.Equal(id, DesignTimeDbContextFactory.ApiUserSecretsId);
    }
}
