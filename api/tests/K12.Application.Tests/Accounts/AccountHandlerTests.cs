using K12.Application.Accounts;
using K12.Domain.Accounts;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.Accounts;

public class AccountHandlerTests
{
    private sealed class FakeGoogle(bool configured = true) : IExternalLoginVerifier
    {
        public string Provider => "google";
        public bool IsConfigured => configured;
        public string? ClientId => configured ? "client.apps.googleusercontent.com" : null;

        /// <summary>Accepts "good:subject:email" (and "good:subject:email:authoritative") tokens and refuses everything else.</summary>
        public Task<VerifiedLogin?> VerifyAsync(string token, CancellationToken ct = default)
        {
            var parts = token.Split(':');
            return Task.FromResult(parts switch
            {
                ["good", var sub, var email] => new VerifiedLogin("google", sub, email, "Pat"),
                ["good", var sub, var email, "authoritative"] => new VerifiedLogin("google", sub, email, "Pat", EmailIsAuthoritative: true),
                _ => null,
            });
        }
    }

    private static SignInWithProviderHandler SignIn(TestStore s, bool configured = true, SignInAllowList? allowList = null) =>
        new([new FakeGoogle(configured)], s, s, s, allowList ?? SignInAllowList.Open);

    [Theory]
    [InlineData(false, "", "stranger@example.org", null)]
    [InlineData(true, "pat@example.org", "pat@example.org", null)]
    [InlineData(true, "pat@example.org", "PAT@Example.org", null)]
    [InlineData(true, "pat@example.org,sam@gmail.com", "sam@gmail.com", null)]
    [InlineData(true, "pat@example.org", "stranger@example.org", "auth.not_invited")]
    [InlineData(true, "", "pat@example.org", "auth.not_invited")]
    public async Task Invite_only_sign_in_admits_only_listed_emails(bool inviteOnly, string allowed, string email, string? error)
    {
        var s = new TestStore();
        var allowList = new SignInAllowList(inviteOnly, allowed.Split(',', StringSplitOptions.RemoveEmptyEntries));
        var result = await SignIn(s, allowList: allowList).HandleAsync(new SignInWithProviderCommand("google", $"good:1:{email}"));
        Assert.Equal(error, result.Error?.Code);
        Assert.Equal(error is null ? 1 : 0, s.Accounts.Count);
    }

    [Theory]
    [InlineData("google", "good:1:pat@example.org", true, null)]
    [InlineData("GOOGLE", "good:1:pat@example.org", true, null)]
    [InlineData("google", "forged", true, "auth.invalid_token")]
    [InlineData("google", "", true, "auth.missing_token")]
    [InlineData("google", "good:1:pat@example.org", false, "auth.provider_not_configured")]
    [InlineData("facebook", "good:1:pat@example.org", true, "auth.provider_not_configured")]
    [InlineData("google", "good:1:pat@example.org:oversized", true, "auth.invalid_token")]
    public async Task Sign_in_needs_a_configured_provider_and_a_token_it_accepts(string provider, string token, bool configured, string? error)
    {
        var s = new TestStore();
        if (token.EndsWith(":oversized")) token = "good:1:pat@example.org" + new string('x', SignInWithProviderHandler.MaxTokenLength);
        var result = await SignIn(s, configured).HandleAsync(new SignInWithProviderCommand(provider, token));
        Assert.Equal(error, result.Error?.Code);
        Assert.Equal(error is null ? 1 : 0, s.Accounts.Count);
    }

    [Fact]
    public async Task Signing_in_again_finds_the_same_account_by_subject_even_with_a_new_email()
    {
        var s = new TestStore();
        var first = await SignIn(s).HandleAsync(new SignInWithProviderCommand("google", "good:1:pat@example.org"));
        var again = await SignIn(s).HandleAsync(new SignInWithProviderCommand("google", "good:1:pat.new@example.org"));

        Assert.Equal(first.Value!.Account.Id, again.Value!.Account.Id);
        Assert.Equal("pat.new@example.org", again.Value.Account.Email);
        Assert.Single(s.Accounts);
        Assert.Equal(2, s.Saves);
    }

    [Theory]
    [InlineData("good:1:pat@gmail.com:authoritative", true)]
    [InlineData("good:1:pat@example.org", false)]
    public async Task Sign_in_says_whether_the_provider_owns_the_email(string token, bool authoritative)
    {
        var result = await SignIn(new TestStore()).HandleAsync(new SignInWithProviderCommand("google", token));
        Assert.Equal(authoritative, result.Value!.EmailIsAuthoritative);
    }

    [Theory]
    [InlineData("dev@example.org", null)]
    [InlineData("nope", "auth.invalid_email")]
    public async Task Developer_sign_in_needs_an_email(string email, string? error)
    {
        var s = new TestStore();
        var result = await new SignInAsDeveloperHandler(s, s, s).HandleAsync(new SignInAsDeveloperCommand(email, null));
        Assert.Equal(error, result.Error?.Code);
        if (error is null) Assert.Equal(new ExternalLogin("developer", email), s.Accounts.Single().Login);
    }

    private static (TestStore Store, ParentAccount Account, Course Course) Family()
    {
        var s = new TestStore();
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", s.UtcNow);
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        s.Accounts.Add(account);
        s.Courses.Add(course);
        return (s, account, course);
    }

    [Fact]
    public async Task A_parent_adds_changes_and_removes_kids()
    {
        var (s, account, course) = Family();

        var added = await new AddKidHandler(s, s, s).HandleAsync(new AddKidCommand(account.Id.Value, "Maya", AvatarColor.Orange, course.Id.Value));
        Assert.Equal(("Maya", AvatarColor.Orange, (Guid?)course.Id.Value), (added.Value!.Name, added.Value.Avatar, added.Value.CourseId));

        var changed = await new UpdateKidHandler(s, s, s).HandleAsync(new UpdateKidCommand(account.Id.Value, added.Value.Id, "Maya R", AvatarColor.Teal, null));
        Assert.Equal(("Maya R", (Guid?)null), (changed.Value!.Name, changed.Value.CourseId));

        var chosen = await new ChooseKidCourseHandler(s, s, s).HandleAsync(new ChooseKidCourseCommand(account.Id.Value, added.Value.Id, course.Id.Value));
        Assert.Equal(course.Id.Value, chosen.Value!.CourseId);

        var view = await new GetAccountHandler(s).HandleAsync(new GetAccountQuery(account.Id.Value));
        Assert.Single(view.Value!.Kids);

        var removed = await new RemoveKidHandler(s, s, s, s).HandleAsync(new RemoveKidCommand(account.Id.Value, added.Value.Id));
        Assert.Empty(removed.Value!.Kids);
    }

    public static TheoryData<string, Func<TestStore, ParentAccount, Course, Task<string?>>> FamilyErrors => new()
    {
        { "family.no_account", async (s, _, c) => (await new AddKidHandler(s, s, s).HandleAsync(new AddKidCommand(Guid.NewGuid(), "Maya", AvatarColor.Blue, null))).Error?.Code },
        { "family.no_course", async (s, a, _) => (await new AddKidHandler(s, s, s).HandleAsync(new AddKidCommand(a.Id.Value, "Maya", AvatarColor.Blue, Guid.NewGuid()))).Error?.Code },
        { "family.invalid", async (s, a, _) => (await new AddKidHandler(s, s, s).HandleAsync(new AddKidCommand(a.Id.Value, "maya@example.org", AvatarColor.Blue, null))).Error?.Code },
        { "family.no_kid", async (s, a, _) => (await new UpdateKidHandler(s, s, s).HandleAsync(new UpdateKidCommand(a.Id.Value, Guid.NewGuid(), "Maya", AvatarColor.Blue, null))).Error?.Code },
        { "family.no_kid", async (s, a, c) => (await new ChooseKidCourseHandler(s, s, s).HandleAsync(new ChooseKidCourseCommand(a.Id.Value, Guid.NewGuid(), c.Id.Value))).Error?.Code },
        { "family.no_kid", async (s, a, _) => (await new RemoveKidHandler(s, s, s, s).HandleAsync(new RemoveKidCommand(a.Id.Value, Guid.NewGuid()))).Error?.Code },
    };

    [Theory]
    [MemberData(nameof(FamilyErrors))]
    public async Task Family_changes_report_what_is_wrong_and_save_nothing(string expected, Func<TestStore, ParentAccount, Course, Task<string?>> act)
    {
        var (s, account, course) = Family();
        Assert.Equal(expected, await act(s, account, course));
        Assert.Equal(0, s.Saves);
    }
}
