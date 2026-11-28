using K12.Domain.Accounts;
using K12.Domain.Common;

namespace K12.Domain.Tests.Accounts;

public class ParentPinTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 5, 0, 0, TimeSpan.Zero);

    private static ParentAccount WithPin()
    {
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", At);
        account.SetPin("hash", At);
        return account;
    }

    [Theory]
    [InlineData("1234", "1234")]
    [InlineData(" 0007 ", "0007")]
    public void A_pin_is_four_digits(string typed, string expected) => Assert.Equal(expected, ParentPin.Valid(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("١٢٣٤")] // digits from another script are not on the PIN pad
    public void Anything_else_is_refused(string? typed) => Assert.Throws<DomainException>(() => ParentPin.Valid(typed));

    [Fact]
    public void No_pin_yet_says_so()
    {
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", At);
        Assert.False(account.HasPin);
        Assert.Equal(PinCheck.NoPin, account.CheckPin(true, At));
    }

    /// <summary>Each try in order: true for the right PIN. The last column is the result of the final try.</summary>
    [Theory]
    [InlineData(new[] { true }, PinCheck.Unlocked, 0)]
    [InlineData(new[] { false }, PinCheck.Wrong, 1)]
    [InlineData(new[] { false, false, false, false, true }, PinCheck.Unlocked, 0)] // a right PIN resets the count
    [InlineData(new[] { false, false, false, false, false }, PinCheck.LockedOut, 5)]
    [InlineData(new[] { false, false, false, false, false, true }, PinCheck.LockedOut, 5)] // even the right PIN waits out the lock
    public void Wrong_tries_lock_the_pin(bool[] tries, PinCheck last, int wrongTries)
    {
        var account = WithPin();
        var result = PinCheck.NoPin;
        foreach (var right in tries) result = account.CheckPin(right, At);
        Assert.Equal((last, wrongTries), (result, account.Pin!.WrongTries));
    }

    [Fact]
    public void The_lock_ends_after_fifteen_minutes_and_the_count_starts_again()
    {
        var account = WithPin();
        for (var i = 0; i < ParentPin.MaxWrongTries; i++) account.CheckPin(false, At);
        Assert.True(account.Pin!.IsLocked(At.AddMinutes(14)));
        Assert.False(account.Pin.IsLocked(At + ParentPin.LockFor));

        Assert.Equal(PinCheck.Wrong, account.CheckPin(false, At + ParentPin.LockFor));
        Assert.Equal(1, account.Pin.WrongTries);
        Assert.Equal(PinCheck.Unlocked, account.CheckPin(true, At + ParentPin.LockFor));
    }

    [Fact]
    public void Changing_the_pin_clears_wrong_tries()
    {
        var account = WithPin();
        account.CheckPin(false, At);
        account.SetPin("new-hash", At.AddMinutes(1));
        Assert.Equal(("new-hash", 0), (account.Pin!.Hash, account.Pin.WrongTries));
    }
}
