using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class ReleasedRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly AnswerSpec Key = new() { Kind = AnswerKind.Choice, Value = "B" };
    private const string Old = """{"image":"f/01.png","stem":"raw","textNeedsReview":true}""";
    private const string New = """{"image":"f/01.png","stem":"raw","textNeedsReview":false,"choicesAreText":true}""";

    private static Question Released(QuestionOrigin origin = QuestionOrigin.Official)
    {
        var q = Question.Draft([SkillId.New()], "choice", Old, Key, Difficulty.OnLevel, origin);
        if (origin is QuestionOrigin.Official) q.PublishReleased(Now);
        return q;
    }

    public static TheoryData<string, Func<Question>, string, bool, string> Cases => new()
    {
        { "new reading of an untouched item", () => Released(), New, true, New },
        { "same reading again", () => Released(), Old, false, Old },
        {
            "an owner's correction is kept",
            () => { var q = Released(); q.Revise("""{"stem":"fixed"}""", Key); return q; },
            New, false, """{"stem":"fixed"}"""
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_released_question_takes_a_newer_reading_unless_someone_edited_it(
        string _, Func<Question> make, string content, bool changed, string expected)
    {
        var q = make();
        var version = q.Version;

        Assert.Equal(changed, q.RefreshReleasedContent(content, Key with { Accept = [] }));
        Assert.Equal(expected, q.ContentJson);
        Assert.Equal(version, q.Version);
        Assert.Equal(QuestionStatus.Live, q.Status);
    }

    public static TheoryData<string, Func<Question>, string, AnswerSpec> Refused => new()
    {
        { "a different key", () => Released(), New, Key with { Value = "C" } },
        { "a generated question", () => Released(QuestionOrigin.Generated), New, Key },
        { "content that is not an object", () => Released(), "[1]", Key },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_refresh_never_changes_the_key_or_touches_generated_questions(
        string _, Func<Question> make, string content, AnswerSpec key)
    {
        var q = make();
        Assert.Throws<DomainException>(() => q.RefreshReleasedContent(content, key));
        Assert.Equal(Old, q.ContentJson);
    }
}
