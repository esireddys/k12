using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Common;

namespace K12.Application.Accounts;

/// <summary>A parent signs in with a token from an identity provider, for example Google.</summary>
public sealed record SignInWithProviderCommand(string Provider, string Token);

/// <summary>Local development only: signs in by email with no provider, so the app works before Google is set up.</summary>
public sealed record SignInAsDeveloperCommand(string Email, string? Name);

/// <summary>
/// Verifies the provider's token, then finds the parent's account by the provider's subject id,
/// creating it on the first sign-in. The API turns the result into its own session cookie.
/// </summary>
public sealed class SignInWithProviderHandler(
    IEnumerable<IExternalLoginVerifier> verifiers, IParentAccountRepository accounts, IClock clock, IUnitOfWork unitOfWork,
    SignInAllowList allowList)
    : ICommandHandler<SignInWithProviderCommand, SignedIn>
{
    /// <summary>ID tokens are a couple of kilobytes; anything far bigger is refused before it reaches the provider's checks.</summary>
    public const int MaxTokenLength = 8192;

    public async Task<Result<SignedIn>> HandleAsync(SignInWithProviderCommand command, CancellationToken ct = default)
    {
        var verifier = verifiers.FirstOrDefault(v => string.Equals(v.Provider, command.Provider, StringComparison.OrdinalIgnoreCase));
        if (verifier is not { IsConfigured: true })
            return Error.Validation("auth.provider_not_configured", $"Sign-in with {command.Provider} is not set up on this server.");
        if (string.IsNullOrWhiteSpace(command.Token))
            return Error.Validation("auth.missing_token", "No sign-in token was sent.");
        if (command.Token.Length > MaxTokenLength)
            return Error.Validation("auth.invalid_token", "That sign-in could not be verified. Try again.");

        var login = await verifier.VerifyAsync(command.Token, ct);
        if (login is null)
            return Error.Validation("auth.invalid_token", "That sign-in could not be verified. Try again.");
        if (!allowList.Admits(login))
            return Error.Forbidden("auth.not_invited",
                "This app is in private testing for now, so only invited families can sign in. You can still practice as a guest.");

        return await AccountSignIn.FindOrRegisterAsync(login, accounts, clock, unitOfWork, ct);
    }
}

public sealed class SignInAsDeveloperHandler(IParentAccountRepository accounts, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SignInAsDeveloperCommand, SignedIn>
{
    public const string Provider = "developer";

    public async Task<Result<SignedIn>> HandleAsync(SignInAsDeveloperCommand command, CancellationToken ct = default)
    {
        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!email.Contains('@')) return Error.Validation("auth.invalid_email", "Enter an email address.");
        return await AccountSignIn.FindOrRegisterAsync(
            new VerifiedLogin(Provider, email, email, command.Name ?? string.Empty, EmailIsAuthoritative: true), accounts, clock, unitOfWork, ct);
    }
}

internal static class AccountSignIn
{
    /// <remarks>The developer sign-in exists only in Development, where any typed email is trusted on purpose.</remarks>
    public static async Task<Result<SignedIn>> FindOrRegisterAsync(
        VerifiedLogin login, IParentAccountRepository accounts, IClock clock, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        try
        {
            var key = new ExternalLogin(login.Provider, login.Subject);
            var account = await accounts.FindByLoginAsync(key, ct);
            if (account is null)
            {
                account = ParentAccount.Register(key, login.Email, login.Name, clock.UtcNow);
                await accounts.AddAsync(account, ct);
            }
            else
            {
                account.RecordSignIn(login.Email, login.Name, clock.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new SignedIn(AccountViews.From(account), login.EmailIsAuthoritative);
        }
        catch (DomainException e)
        {
            return Error.Validation("auth.invalid_login", e.Message);
        }
    }
}
