using Google.Apis.Auth;
using K12.Application.Accounts;
using Microsoft.Extensions.Logging;

namespace K12.Infrastructure.Identity;

/// <summary>
/// Checks a Google ID token from "Sign in with Google" in the browser: Google's signature, that Google issued it,
/// that it was issued for this app's client id, that it has not expired, and that Google verified the email.
/// No client secret is involved, so the only setting is the public client id (Auth:Google:ClientId).
/// </summary>
public sealed class GoogleLoginVerifier(string? clientId, ILogger<GoogleLoginVerifier> logger) : IExternalLoginVerifier
{
    /// <summary>Allows for a home PC whose clock is a little off.</summary>
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(2);

    public string Provider => "google";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(clientId);
    public string? ClientId => IsConfigured ? clientId!.Trim() : null;

    public async Task<VerifiedLogin?> VerifyAsync(string token, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [ClientId!],
                IssuedAtClockTolerance = ClockTolerance,
                ExpirationTimeClockTolerance = ClockTolerance,
            });
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email)) return null;
            return new VerifiedLogin(
                Provider, payload.Subject, payload.Email, payload.Name ?? string.Empty,
                IsAuthoritative(payload.Email, payload.HostedDomain));
        }
        catch (InvalidJwtException e)
        {
            logger.LogInformation("Refused a Google sign-in: {Reason}", e.Message);
            return null;
        }
    }

    /// <summary>
    /// Whether Google owns the mailbox, so the email really is this person's today: a Gmail address, or a Google
    /// Workspace account (the token's "hd" claim) whose email is in that domain. Other addresses on a Google account
    /// were verified once and may have changed hands since, so they never earn owner rights.
    /// </summary>
    public static bool IsAuthoritative(string email, string? hostedDomain)
    {
        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        var domain = email[(at + 1)..].Trim();
        if (domain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase)
            || domain.Equals("googlemail.com", StringComparison.OrdinalIgnoreCase)) return true;
        return !string.IsNullOrWhiteSpace(hostedDomain) && domain.Equals(hostedDomain.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
