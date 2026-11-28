namespace K12.Application.Accounts;

/// <summary>
/// Who may sign in. Open by default. While the app is in private testing it is invite only: only the listed emails
/// (and the owners) can sign in, so a family that finds the address cannot make an account. Guests are not affected.
/// </summary>
public sealed class SignInAllowList
{
    private readonly HashSet<string> _emails;

    public SignInAllowList(bool inviteOnly, IEnumerable<string> allowedEmails)
    {
        InviteOnly = inviteOnly;
        _emails = new HashSet<string>(
            allowedEmails.Select(e => e.Trim()).Where(e => e.Length > 0), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Everyone may sign in.</summary>
    public static SignInAllowList Open { get; } = new(false, []);

    public bool InviteOnly { get; }

    /// <summary>True when this verified login may sign in.</summary>
    public bool Admits(VerifiedLogin login) => !InviteOnly || _emails.Contains(login.Email.Trim());
}
