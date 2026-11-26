using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace K12.Api.Auth;

/// <summary>
/// Who may do what. Owners run the app and approve content; they are never parents.
/// The review agent is the AI reviewer job, which may only read its queue and record findings.
/// </summary>
public static class Access
{
    public const string OwnerRole = "owner";
    public const string ReviewAgentRole = "review-agent";

    public const string OwnerPolicy = "Owner";
    public const string ReviewAgentPolicy = "ReviewAgent";

    public const string DevelopmentScheme = "Development";
    public const string NoSignInScheme = "NoSignIn";

    /// <summary>The header a local run can send to act as someone else, for example a parent, to see the owner screens refuse.</summary>
    public const string DevelopmentUserHeader = "X-Dev-User";

    /// <summary>
    /// Development signs every request in as a local user so the owner screens work before sign-in exists.
    /// Everywhere else nobody is signed in until Entra External ID is wired up, so owner endpoints answer 401.
    /// </summary>
    public static IServiceCollection AddK12Access(this IServiceCollection services, IHostEnvironment environment)
    {
        var scheme = environment.IsDevelopment() ? DevelopmentScheme : NoSignInScheme;
        var auth = services.AddAuthentication(scheme);
        if (environment.IsDevelopment())
            auth.AddScheme<AuthenticationSchemeOptions, DevelopmentSignIn>(DevelopmentScheme, null);
        else
            auth.AddScheme<AuthenticationSchemeOptions, NoSignIn>(NoSignInScheme, null);

        services.AddTransient<IClaimsTransformation, RolesFromConfiguration>();
        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerPolicy, p => p.RequireAuthenticatedUser().RequireRole(OwnerRole))
            .AddPolicy(ReviewAgentPolicy, p => p.RequireAuthenticatedUser().RequireRole(OwnerRole, ReviewAgentRole));
        return services;
    }

    /// <summary>The name decisions are recorded under: the email when there is one.</summary>
    public static string ReviewerName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "unknown";
}

/// <summary>
/// Grants roles from configuration (Access:Owners and Access:ReviewAgents, lists of emails or names),
/// whatever signed the user in. Keeps the owner list out of code and per environment.
/// </summary>
internal sealed class RolesFromConfiguration(IConfiguration configuration) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity) return Task.FromResult(principal);
        var who = principal.ReviewerName();
        Grant(identity, who, "Access:Owners", Access.OwnerRole);
        Grant(identity, who, "Access:ReviewAgents", Access.ReviewAgentRole);
        return Task.FromResult(principal);
    }

    private void Grant(ClaimsIdentity identity, string who, string key, string role)
    {
        var names = configuration.GetSection(key).Get<string[]>() ?? [];
        if (names.Contains(who, StringComparer.OrdinalIgnoreCase) && !identity.HasClaim(ClaimTypes.Role, role))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }
}

/// <summary>Development only: signs the request in as Access:DevelopmentUser, or as the X-Dev-User header if sent.</summary>
internal sealed class DevelopmentSignIn(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[Access.DevelopmentUserHeader].ToString();
        var user = string.IsNullOrWhiteSpace(header) ? configuration["Access:DevelopmentUser"] ?? "owner@localhost" : header.Trim();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user), new Claim(ClaimTypes.Email, user)], Access.DevelopmentScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

/// <summary>Until real sign-in is configured outside Development, nobody is signed in.</summary>
internal sealed class NoSignIn(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
