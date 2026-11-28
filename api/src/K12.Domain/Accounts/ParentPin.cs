using K12.Domain.Common;

namespace K12.Domain.Accounts;

/// <summary>What happened when someone typed the parent PIN.</summary>
public enum PinCheck
{
    /// <summary>Right PIN: the parent areas open.</summary>
    Unlocked,
    /// <summary>Wrong PIN; more tries are left.</summary>
    Wrong,
    /// <summary>Too many wrong tries: no PIN is accepted until the lock ends, even the right one.</summary>
    LockedOut,
    /// <summary>The parent has not set a PIN yet.</summary>
    NoPin,
}

/// <summary>
/// The parent's 4-digit PIN, which keeps kids out of the parent areas on a shared tablet. It is not a sign-in: the
/// parent's Google sign-in is. Only a hash is kept. Five wrong tries lock it for 15 minutes, so the 10,000 PINs
/// cannot be tried one by one.
/// </summary>
public sealed class ParentPin
{
    public const int Length = 4;
    public const int MaxWrongTries = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    /// <summary>For the persistence layer only.</summary>
    private ParentPin() => Hash = string.Empty;

    internal ParentPin(string hash, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new DomainException("A PIN needs a hash.");
        Hash = hash;
        SetAt = at;
    }

    public string Hash { get; private set; }
    public DateTimeOffset SetAt { get; private set; }
    public int WrongTries { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Checks the shape of a PIN someone typed, before it is hashed: exactly four digits.</summary>
    public static string Valid(string? pin)
    {
        var trimmed = (pin ?? string.Empty).Trim();
        if (trimmed.Length != Length || !trimmed.All(char.IsAsciiDigit))
            throw new DomainException($"A parent PIN is {Length} digits.");
        return trimmed;
    }

    public bool IsLocked(DateTimeOffset at) => LockedUntil is { } until && until > at;

    internal PinCheck Check(bool matches, DateTimeOffset at)
    {
        if (IsLocked(at)) return PinCheck.LockedOut;
        if (LockedUntil is not null)
        {
            // The lock has run out: start counting again.
            LockedUntil = null;
            WrongTries = 0;
        }

        if (matches)
        {
            WrongTries = 0;
            return PinCheck.Unlocked;
        }

        WrongTries++;
        if (WrongTries < MaxWrongTries) return PinCheck.Wrong;
        LockedUntil = at + LockFor;
        return PinCheck.LockedOut;
    }
}
