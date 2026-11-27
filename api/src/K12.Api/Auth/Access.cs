using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace K12.Api.Auth;

/// <summary>
/// Who may do what. Owners run the app and approve content. Owner rights come only from Access:Owners:
/// signing in as a parent never grants them.
/// The review agent is the AI reviewer job, which may only read its queue and record findings.
/// </summary>
public static class Access
{
    public const string OwnerRole = "owner";
    public const string ReviewAgentRole = "review-agent";

    public const string OwnerPolicy = "Owner";
    public const string ReviewAgentPolicy = "ReviewAgent";

    public const string SessionScheme = "K12Session";
    public const string DevelopmentScheme = "Development";
    private const string SelectScheme = "K12";

    public const string ParentPolicy = "Parent";

    /// <summary>The claim holding the signed-in parent's account id. Only cookie sessions carry it.</summary>
    public const string AccountIdClaim = "k12:account";

    /// <summary>The header a local run can send to act as someone else, for example a parent, to see the owner screens refuse.</summary>
    public const string DevelopmentUserHeader = "X-Dev-User";

    /// <summary>
    /// A parent signs in with Google (or, locally, the developer sign-in); the API then keeps them signed in with
    /// its own HttpOnly session cookie, so no token is stored in the browser. Owners sign in the same way and are
    /// recognised by email from Access:Owners.
    /// In Development a request with no cookie can still act as a user, for tests and scripts: the X-Dev-User header,
    /// or Access:DevelopmentUser when it is set.
    /// </summary>
    public static IServiceCollection AddK12Access(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        var auth = services.AddAuthentication(SelectScheme);
        auth.AddPolicyScheme(SelectScheme, "Session or development user", o => o.ForwardDefaultSelector = http =>
        {
            if (!environment.IsDevelopment() || http.Request.Cookies.ContainsKey(SessionCookieName)) return SessionScheme;
            var devUser = http.Request.Headers.ContainsKey(DevelopmentUserHeader)
                || !string.IsNullOrWhiteSpace(configuration["Access:DevelopmentUser"]);
            return devUser ? DevelopmentScheme : SessionScheme;
        });
        auth.AddCookie(SessionScheme, o =>
        {
            o.Cookie.Name = SessionCookieName;
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            // Lax works when the web app and the API share a site (localhost, or one domain behind Front Door).
            o.Cookie.SameSite = Enum.TryParse<SameSiteMode>(configuration["Auth:Cookie:SameSite"], out var mode) ? mode : SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromDays(30);
            o.SlidingExpiration = true;
            // An API answers 401 and 403; it never redirects to a login page.
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });
        if (environment.IsDevelopment())
            auth.AddScheme<AuthenticationSchemeOptions, DevelopmentSignIn>(DevelopmentScheme, null);

        services.AddTransient<IClaimsTransformation, RolesFromConfiguration>();
        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerPolicy, p => p.RequireAuthenticatedUser().RequireRole(OwnerRole))
            .AddPolicy(ReviewAgentPolicy, p => p.RequireAuthenticatedUser().RequireRole(OwnerRole, ReviewAgentRole))
            .AddPolicy(ParentPolicy, p => p.RequireAuthenticatedUser().RequireClaim(AccountIdClaim));
        return services;
    }

    public const string SessionCookieName = "k12.session";

    /// <summary>
    /// Until when the parent areas are open on this session, as Unix seconds. Set by the parent PIN or a fresh
    /// sign-in, and kept in the session cookie, which the server encrypts and signs, so a kid cannot forge it.
    /// </summary>
    public const string ParentAreaOpenUntilClaim = "k12:parent-open-until";

    /// <summary>How long the parent areas stay open after the PIN, before the PIN is asked again.</summary>
    public static readonly TimeSpan ParentAreaWindow = TimeSpan.FromMinutes(10);

    /// <summary>The claims a session cookie carries for a signed-in parent.</summary>
    /// <param name="parentAreaOpenUntil">Set when the parent areas are open on this session.</param>
    public static ClaimsPrincipal SessionPrincipal(Guid accountId, string displayName, string email, DateTimeOffset? parentAreaOpenUntil = null)
    {
        var claims = new List<Claim>
        {
            new(AccountIdClaim, accountId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
        };
        if (parentAreaOpenUntil is { } until)
            claims.Add(new Claim(ParentAreaOpenUntilClaim, until.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, SessionScheme));
    }

    /// <summary>Until when the parent areas are open on this session, if they are open at <paramref name="now"/>.</summary>
    public static DateTimeOffset? ParentAreaOpenUntil(this ClaimsPrincipal user, DateTimeOffset now) =>
        long.TryParse(user.FindFirstValue(ParentAreaOpenUntilClaim), out var seconds)
        && DateTimeOffset.FromUnixTimeSeconds(seconds) is var until && until > now
            ? until
            : null;

    /// <summary>The signed-in parent's account id. Only call it behind <see cref="ParentPolicy"/>.</summary>
    public static Guid AccountId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(AccountIdClaim)!);

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

/// <summary>Development only, with no session cookie: signs the request in as the X-Dev-User header, or Access:DevelopmentUser.</summary>
internal sealed class DevelopmentSignIn(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[Access.DevelopmentUserHeader].ToString();
        var user = string.IsNullOrWhiteSpace(header) ? configuration["Access:DevelopmentUser"] ?? "" : header.Trim();
        if (string.IsNullOrWhiteSpace(user)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user), new Claim(ClaimTypes.Email, user)], Access.DevelopmentScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
