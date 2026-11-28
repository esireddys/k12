using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Accounts;

public class ParentAccountTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 5, 0, 0, TimeSpan.Zero);

    private static ParentAccount Parent() =>
        ParentAccount.Register(new ExternalLogin("Google", " 1234 "), "Parent@Example.org", "Pat Parent", At);

    [Fact]
    public void Registering_normalises_the_login_and_email_and_raises_an_event()
    {
        var account = Parent();
        Assert.Equal(new ExternalLogin("google", "1234"), account.Login);
        Assert.Equal("parent@example.org", account.Email);
        Assert.Equal("Pat Parent", account.DisplayName);
        Assert.Equal(At, account.CreatedAt);
        Assert.IsType<ParentRegistered>(Assert.Single(account.DomainEvents));
    }

    [Fact]
    public void A_parent_with_no_name_is_called_by_the_start_of_their_email()
    {
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "sam@example.org", " ", At);
        Assert.Equal("sam", account.DisplayName);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("google", "")]
    [InlineData("", "123")]
    public void A_login_needs_a_provider_and_a_subject(string provider, string subject) =>
        Assert.Throws<DomainException>(() => new ExternalLogin(provider, subject));

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void An_account_needs_an_email(string email) =>
        Assert.Throws<DomainException>(() => ParentAccount.Register(new ExternalLogin("google", "1"), email, "x", At));

    [Fact]
    public void A_later_sign_in_updates_the_email_name_and_time()
    {
        var account = Parent();
        account.RecordSignIn("new@example.org", "Pat P", At.AddDays(1));
        Assert.Equal(("new@example.org", "Pat P", At.AddDays(1)), (account.Email, account.DisplayName, account.LastSignedInAt));
    }

    [Theory]
    [InlineData("Maya", true)]
    [InlineData("  Maya  ", true)]
    [InlineData("Ana-Lucía O'Neil", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("maya@example.org", false)]
    [InlineData("https://example.org", false)]
    [InlineData("a/b", false)]
    [InlineData("This name is much longer than thirty", false)]
    public void Kid_names_are_a_first_name_or_nickname(string name, bool ok)
    {
        var account = Parent();
        if (ok) Assert.Equal(name.Trim(), account.AddKid(name, AvatarColor.Blue, null).Name);
        else Assert.Throws<DomainException>(() => account.AddKid(name, AvatarColor.Blue, null));
    }

    [Fact]
    public void Kid_names_are_unique_in_a_family_ignoring_case()
    {
        var account = Parent();
        account.AddKid("Maya", AvatarColor.Blue, null);
        Assert.Throws<DomainException>(() => account.AddKid("maya", AvatarColor.Green, null));
    }

    [Fact]
    public void A_family_has_at_most_eight_kids()
    {
        var account = Parent();
        for (var i = 0; i < ParentAccount.MaxKids; i++) account.AddKid($"Kid {i}", AvatarColor.Teal, null);
        Assert.Throws<DomainException>(() => account.AddKid("One more", AvatarColor.Teal, null));
    }

    [Fact]
    public void An_unknown_picture_colour_is_refused() =>
        Assert.Throws<DomainException>(() => Parent().AddKid("Maya", (AvatarColor)99, null));

    [Fact]
    public void A_kid_can_be_changed_given_a_course_and_removed()
    {
        var account = Parent();
        var kid = account.AddKid("Maya", AvatarColor.Blue, null);
        var other = account.AddKid("Leo", AvatarColor.Green, null);
        var course = CourseId.New();

        account.UpdateKid(kid.Id, "Maya R", AvatarColor.Pink, null);
        account.ChooseCourse(kid.Id, course);
        Assert.Equal(("Maya R", AvatarColor.Pink, (CourseId?)course), (kid.Name, kid.Avatar, kid.CourseId));

        Assert.Throws<DomainException>(() => account.UpdateKid(kid.Id, "Leo", AvatarColor.Pink, null));

        account.RemoveKid(other.Id);
        Assert.Equal([kid], account.Kids);
        Assert.Throws<DomainException>(() => account.RemoveKid(other.Id));
    }
}
