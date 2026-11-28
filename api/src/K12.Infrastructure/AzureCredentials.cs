using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace K12.Infrastructure;

/// <summary>
/// How the app signs in to Azure services (Blob Storage, Key Vault). In Azure it is the app's user-assigned managed
/// identity, named by AZURE_CLIENT_ID; on a developer machine, whoever ran "az login". Never a key or a password.
/// </summary>
public static class AzureCredentials
{
    public static TokenCredential Create(IConfiguration configuration) => new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = configuration["AZURE_CLIENT_ID"],
        ExcludeInteractiveBrowserCredential = true,
    });
}
