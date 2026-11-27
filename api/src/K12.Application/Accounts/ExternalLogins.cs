namespace K12.Application.Accounts;

/// <summary>A sign-in the identity provider vouched for.</summary>
public sealed record VerifiedLogin(string Provider, string Subject, string Email, string Name);

/// <summary>
/// Checks a token from an identity provider, for example a Google ID token from the browser.
/// Implemented in Infrastructure; Entra External ID plugs in here when the app moves to Azure.
/// </summary>
public interface IExternalLoginVerifier
{
    /// <summary>The provider name, for example "google".</summary>
    string Provider { get; }

    /// <summary>False when the app has no client id for this provider, so its button should not be shown.</summary>
    bool IsConfigured { get; }

    /// <summary>The client id the browser needs to start this provider's sign-in. Public, never a secret.</summary>
    string? ClientId { get; }

    /// <summary>The verified login, or null when the token is invalid, expired, for another app, or its email is unverified.</summary>
    Task<VerifiedLogin?> VerifyAsync(string token, CancellationToken ct = default);
}
