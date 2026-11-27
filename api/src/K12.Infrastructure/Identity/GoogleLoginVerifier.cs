using Google.Apis.Auth;
using K12.Application.Accounts;
using Microsoft.Extensions.Logging;

namespace K12.Infrastructure.Identity;

/// <summary>
/// Checks a Google ID token from "Sign in with Google" in the browser: Google's signature, that it was issued
/// for this app's client id, that it has not expired, and that Google verified the email.
/// No client secret is involved, so the only setting is the public client id (Auth:Google:ClientId).
/// </summary>
public sealed class GoogleLoginVerifier(string? clientId, ILogger<GoogleLoginVerifier> logger) : IExternalLoginVerifier
{
    public string Provider => "google";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(clientId);
    public string? ClientId => IsConfigured ? clientId : null;

    public async Task<VerifiedLogin?> VerifyAsync(string token, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                token, new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId!] });
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email)) return null;
            return new VerifiedLogin(Provider, payload.Subject, payload.Email, payload.Name ?? string.Empty);
        }
        catch (InvalidJwtException e)
        {
            logger.LogInformation("Refused a Google sign-in: {Reason}", e.Message);
            return null;
        }
    }
}
