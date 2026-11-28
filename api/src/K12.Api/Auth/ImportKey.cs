using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace K12.Api.Auth;

/// <summary>
/// Signs in the content pipeline when it imports released tests into a deployed API. The pipeline sends the shared
/// key from Access:ImportKey (Key Vault in Azure) in the X-Import-Key header and gets only the importer role: it can
/// load curriculum and released tests, nothing else. With no key configured, nobody signs in this way.
/// </summary>
internal sealed class ImportKeySignIn(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Header = "X-Import-Key";
    public const string PrincipalName = "content-pipeline";

    /// <summary>Shorter keys are refused, so a placeholder or a typo can never open the import endpoints.</summary>
    public const int MinimumKeyLength = 32;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sent = Request.Headers[Header].ToString();
        if (sent.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        if (!Matches(configuration["Access:ImportKey"], sent))
            return Task.FromResult(AuthenticateResult.Fail("The import key is not valid."));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, PrincipalName), new Claim(ClaimTypes.Role, Access.ImporterRole)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    /// <summary>Compares hashes in constant time, so the response time says nothing about how much of the key was right.</summary>
    internal static bool Matches(string? configured, string sent) =>
        configured is { Length: >= MinimumKeyLength }
        && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(sent)));
}
