using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Accounts;

public readonly record struct ParentAccountId(Guid Value)
{
    public static ParentAccountId New() => new(Guid.NewGuid());
}

/// <summary>
/// The sign-in a parent used, for example ("google", the Google account's subject id).
/// The subject never changes for a person, unlike their email, so it is what identifies them.
/// </summary>
public sealed record ExternalLogin
{
    public ExternalLogin(string provider, string subject)
    {
        if (string.IsNullOrWhiteSpace(provider)) throw new DomainException("A login needs a provider.");
        if (string.IsNullOrWhiteSpace(subject)) throw new DomainException("A login needs a subject.");
        Provider = provider.Trim().ToLowerInvariant();
        Subject = subject.Trim();
    }

    public string Provider { get; }
    public string Subject { get; }
}

public sealed record ParentRegistered(ParentAccountId AccountId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// One family: the parent who signs in, and the kids who practice under that sign-in.
/// Kids never sign in themselves and the account holds nothing about them beyond a first name,
/// a picture colour and the course they practice.
/// </summary>
public sealed class ParentAccount : AggregateRoot<ParentAccountId>
{
    /// <summary>Enough for a large family on one device, small enough to keep the picker on one screen.</summary>
    public const int MaxKids = 8;

    private readonly List<KidProfile> _kids = [];

    /// <summary>For the persistence layer only.</summary>
    private ParentAccount() : base(default)
    {
        Login = null!;
        Email = DisplayName = string.Empty;
    }

    private ParentAccount(ParentAccountId id, ExternalLogin login, string email, string displayName, DateTimeOffset at) : base(id)
    {
        Login = login;
        Email = string.Empty;
        DisplayName = string.Empty;
        CreatedAt = at;
        UpdateProfile(email, displayName, at);
    }

    public ExternalLogin Login { get; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastSignedInAt { get; private set; }
    public IReadOnlyList<KidProfile> Kids => _kids;

    /// <summary>The PIN that guards the parent areas on a shared device. Empty until the parent sets one.</summary>
    public ParentPin? Pin { get; private set; }

    public bool HasPin => Pin is not null;

    /// <summary>
    /// Sets or changes the PIN. Callers check who is asking first: the first PIN can be set by the signed-in parent,
    /// a change needs the parent areas unlocked (by the old PIN or a fresh sign-in).
    /// </summary>
    /// <param name="hash">The hash of a PIN that passed <see cref="ParentPin.Valid"/>. The domain never sees the digits stored.</param>
    public void SetPin(string hash, DateTimeOffset at) => Pin = new ParentPin(hash, at);

    /// <summary>Records one try at the PIN. <paramref name="matches"/> is whether the typed PIN matched the hash.</summary>
    public PinCheck CheckPin(bool matches, DateTimeOffset at) => Pin?.Check(matches, at) ?? PinCheck.NoPin;

    /// <summary>Creates the account the first time a parent signs in.</summary>
    public static ParentAccount Register(ExternalLogin login, string email, string displayName, DateTimeOffset at)
    {
        var account = new ParentAccount(ParentAccountId.New(), login, email, displayName, at);
        account.Raise(new ParentRegistered(account.Id, at));
        return account;
    }

    /// <summary>A later sign-in: the provider may report a changed email or name.</summary>
    public void RecordSignIn(string email, string displayName, DateTimeOffset at) => UpdateProfile(email, displayName, at);

    public KidProfile AddKid(string name, AvatarColor avatar, CourseId? courseId)
    {
        if (_kids.Count >= MaxKids) throw new DomainException($"A family can have at most {MaxKids} kids.");
        var kid = new KidProfile(KidProfileId.New(), name, avatar, courseId);
        EnsureUniqueName(kid.Name, except: null);
        _kids.Add(kid);
        return kid;
    }

    public KidProfile UpdateKid(KidProfileId kidId, string name, AvatarColor avatar, CourseId? courseId)
    {
        var kid = Kid(kidId);
        kid.Change(name, avatar, courseId);
        EnsureUniqueName(kid.Name, except: kidId);
        return kid;
    }

    /// <summary>The kid a learner picked a course for, from the course screen rather than the parent's settings.</summary>
    public void ChooseCourse(KidProfileId kidId, CourseId courseId) => Kid(kidId).ChooseCourse(courseId);

    public void RemoveKid(KidProfileId kidId) => _kids.Remove(Kid(kidId));

    public bool HasKid(KidProfileId kidId) => _kids.Any(k => k.Id == kidId);

    private KidProfile Kid(KidProfileId kidId) =>
        _kids.FirstOrDefault(k => k.Id == kidId) ?? throw new DomainException("That kid is not in this family.");

    private void EnsureUniqueName(string name, KidProfileId? except)
    {
        if (_kids.Any(k => k.Id != except && string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"There is already a kid called {name}.");
    }

    private void UpdateProfile(string email, string displayName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new DomainException("A parent account needs an email.");
        Email = email.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email.Split('@')[0] : displayName.Trim();
        LastSignedInAt = at;
    }
}
