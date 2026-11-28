using K12.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

namespace K12.Api.Hosting;

/// <summary>
/// The keys that encrypt and sign the session cookie. Locally they stay in the user profile. In Azure the app can
/// restart, move or run more than one copy, so the key ring lives in Blob Storage, wrapped by a Key Vault key, and
/// parents stay signed in across deployments.
/// </summary>
public static class DataProtectionHosting
{
    public const string ApplicationName = "k12";

    public static IServiceCollection AddK12DataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName(ApplicationName);

        var keyRing = configuration["DataProtection:KeyRingBlobUri"];
        if (string.IsNullOrWhiteSpace(keyRing)) return services;

        var credential = AzureCredentials.Create(configuration);
        dataProtection.PersistKeysToAzureBlobStorage(new Uri(keyRing), credential);
        if (configuration["DataProtection:KeyVaultKeyUri"] is { Length: > 0 } wrappingKey)
            dataProtection.ProtectKeysWithAzureKeyVault(new Uri(wrappingKey), credential);
        return services;
    }
}
