using K12.Application.Accounts;
using K12.Domain.Accounts;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.Accounts;

public class ParentPinHandlerTests
{
    private static (TestStore Store, ParentAccount Account) Family()
    {
        var s = new TestStore();
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", s.UtcNow);
        s.Accounts.Add(account);
        return (s, account);
    }

    [Fact]
    public async Task The_first_pin_can_be_set_and_only_its_hash_is_kept()
    {
        var (s, account) = Family();
        var result = await new SetPinHandler(s, s, s, s).HandleAsync(new SetPinCommand(account.Id.Value, "4321", ParentAreaOpen: false));
        Assert.True(result.Value!.HasPin);
        Assert.Equal("hash:4321", account.Pin!.Hash);
    }

    [Theory]
    [InlineData(false, "parent.locked")]
    [InlineData(true, null)]
    public async Task Changing_the_pin_needs_the_parent_areas_open(bool open, string? error)
    {
        var (s, account) = Family();
        account.SetPin("hash:1111", s.UtcNow);
        var result = await new SetPinHandler(s, s, s, s).HandleAsync(new SetPinCommand(account.Id.Value, "2222", open));
        Assert.Equal(error, result.Error?.Code);
        Assert.Equal(open ? "hash:2222" : "hash:1111", account.Pin!.Hash);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("abcd")]
    public async Task A_pin_that_is_not_four_digits_is_refused(string pin)
    {
        var (s, account) = Family();
        var result = await new SetPinHandler(s, s, s, s).HandleAsync(new SetPinCommand(account.Id.Value, pin, true));
        Assert.Equal("parent.bad_pin", result.Error?.Code);
    }

    [Theory]
    [InlineData("1111", PinCheck.Unlocked, 5)]
    [InlineData("9999", PinCheck.Wrong, 4)]
    [InlineData("99", PinCheck.Wrong, 4)] // a malformed PIN is just a wrong one
    public async Task Unlocking_checks_the_pin_and_counts_wrong_tries(string typed, PinCheck expected, int triesLeft)
    {
        var (s, account) = Family();
        account.SetPin("hash:1111", s.UtcNow);
        var result = await new UnlockWithPinHandler(s, s, s, s).HandleAsync(new UnlockWithPinCommand(account.Id.Value, typed));
        Assert.Equal((expected, triesLeft), (result.Value!.Result, result.Value.TriesLeft));
    }

    [Fact]
    public async Task Five_wrong_tries_lock_the_pin_and_the_status_shows_until_when()
    {
        var (s, account) = Family();
        account.SetPin("hash:1111", s.UtcNow);
        var unlock = new UnlockWithPinHandler(s, s, s, s);
        for (var i = 0; i < ParentPin.MaxWrongTries; i++) await unlock.HandleAsync(new UnlockWithPinCommand(account.Id.Value, "0000"));

        var locked = await unlock.HandleAsync(new UnlockWithPinCommand(account.Id.Value, "1111"));
        Assert.Equal(PinCheck.LockedOut, locked.Value!.Result);

        var status = await new GetPinStatusHandler(s, s).HandleAsync(new GetPinStatusQuery(account.Id.Value));
        Assert.Equal(s.UtcNow + ParentPin.LockFor, status.Value!.LockedUntil);
    }

    [Theory]
    [InlineData(false, false, null)] // first grade pick: a kid may do it
    [InlineData(true, false, "parent.locked")] // changing it with a PIN set and the parent areas closed
    [InlineData(true, true, null)]
    public async Task Changing_a_kids_grade_is_a_parents_call_once_a_pin_is_set(bool hasCourse, bool open, string? error)
    {
        var (s, account) = Family();
        var first = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", []);
        var second = Course.Create("TX", "math", "A1", "Algebra I", "FW", "Framework", []);
        s.Courses.AddRange([first, second]);
        var kid = account.AddKid("Maya", AvatarColor.Blue, hasCourse ? first.Id : null);
        account.SetPin("hash:1111", s.UtcNow);

        var result = await new ChooseKidCourseHandler(s, s, s).HandleAsync(new ChooseKidCourseCommand(account.Id.Value, kid.Id.Value, second.Id.Value, open));
        Assert.Equal(error, result.Error?.Code);
    }
}

public class SubjectSwitchTests
{
    [Theory]
    [InlineData("reading", "G3", null)]          // another subject in the same grade: the kid's own choice
    [InlineData("math", "A1", "parent.locked")]  // a different grade or course: the parent's call
    [InlineData("reading", "G4", "parent.locked")]
    public async Task A_kid_can_switch_subject_in_their_grade_without_the_parent_pin(string subject, string code, string? error)
    {
        var s = new TestStore();
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", s.UtcNow);
        s.Accounts.Add(account);
        var math = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", []);
        var other = Course.Create("TX", subject, code, "Other", "FW", "Framework", []);
        s.Courses.AddRange([math, other]);
        var kid = account.AddKid("Maya", AvatarColor.Blue, math.Id);
        account.SetPin("hash:1111", s.UtcNow);

        var result = await new ChooseKidCourseHandler(s, s, s).HandleAsync(
            new ChooseKidCourseCommand(account.Id.Value, kid.Id.Value, other.Id.Value, ParentAreaOpen: false));

        Assert.Equal(error, result.Error?.Code);
        Assert.Equal(error is null ? other.Id : math.Id, kid.CourseId);
    }
}
