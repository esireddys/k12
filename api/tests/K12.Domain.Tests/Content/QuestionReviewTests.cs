using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class QuestionReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly AnswerSpec Key = new() { Kind = AnswerKind.Choice, Value = "B" };

    private static Question InAiReview()
    {
        var q = Question.Draft([SkillId.New()], "choice", """{"stem":"raw","textNeedsReview":true}""", Key,
            Difficulty.OnLevel, QuestionOrigin.Official);
        q.SubmitForAiReview();
        return q;
    }

    private static Question AwaitingOwner()
    {
        var q = InAiReview();
        q.RecordAiReview(TestReviews.LooksGood("""{"stem":"clean"}"""));
        return q;
    }

    [Fact]
    public void Recording_an_ai_review_hands_the_question_to_the_owners()
    {
        var q = InAiReview();
        var review = TestReviews.LooksGood("""{"stem":"clean"}""");
        q.RecordAiReview(review);

        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
        Assert.Same(review, q.AiReview);
        Assert.Equal("""{"stem":"raw","textNeedsReview":true}""", q.ContentJson); // proposals never overwrite the content
        Assert.False(q.IsVisibleToStudents);
    }

    public static TheoryData<string, AiReview> IncompleteReviews => new()
    {
        { "no reviewer", TestReviews.LooksGood() with { Reviewer = " " } },
        { "no summary", TestReviews.LooksGood() with { Summary = "" } },
        { "no proposed content", TestReviews.LooksGood() with { ProposedContentJson = "" } },
        { "proposed content is not an object", TestReviews.LooksGood("[1,2]") },
        { "proposed content is not JSON", TestReviews.LooksGood("{stem:") },
        { "key disagrees but looks good", TestReviews.LooksGood() with { AnswerKeyAgrees = false } },
        { "problem finding but looks good", TestReviews.LooksGood("{}", new AiFinding(FindingSeverity.Problem, "stem", "x")) },
    };

    [Theory]
    [MemberData(nameof(IncompleteReviews))]
    public void An_incomplete_or_contradictory_review_is_refused_and_the_question_stays_put(string _, AiReview review)
    {
        var q = InAiReview();
        Assert.Throws<DomainException>(() => q.RecordAiReview(review));
        Assert.Equal(QuestionStatus.InAiReview, q.Status);
        Assert.Null(q.AiReview);
    }

    [Fact]
    public void A_flagged_review_still_reaches_an_owner()
    {
        var q = InAiReview();
        q.RecordAiReview(TestReviews.LooksGood() with
        {
            Verdict = AiVerdict.NeedsAttention, AnswerKeyAgrees = false,
            Findings = [new AiFinding(FindingSeverity.Problem, "answer_key", "Solving gives C, the key says B.")],
        });
        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
    }

    public static TheoryData<QuestionStatus> NotInAiReview => new() { QuestionStatus.Draft, QuestionStatus.AwaitingOwner, QuestionStatus.Live };

    [Theory]
    [MemberData(nameof(NotInAiReview))]
    public void Only_a_question_in_ai_review_takes_a_review(QuestionStatus status)
    {
        var q = Question.Draft([SkillId.New()], "choice", "{}", Key, Difficulty.OnLevel, QuestionOrigin.Official);
        if (status >= QuestionStatus.InAiReview) q.SubmitForAiReview();
        if (status >= QuestionStatus.AwaitingOwner) q.RecordAiReview(TestReviews.LooksGood());
        if (status >= QuestionStatus.Live) q.Approve("owner", Now);

        Assert.Throws<DomainException>(() => q.RecordAiReview(TestReviews.LooksGood()));
    }

    [Fact]
    public void Approving_as_is_keeps_the_version_and_records_the_owner()
    {
        var q = AwaitingOwner();
        q.Approve("owner@example.org", Now, "  fine  ");

        Assert.Equal(QuestionStatus.Live, q.Status);
        Assert.Equal(1, q.Version);
        Assert.Equal(new OwnerDecision(OwnerOutcome.Approved, "owner@example.org", Now, "fine"), q.OwnerDecision);
        Assert.IsType<QuestionApproved>(Assert.Single(q.DomainEvents));
    }

    public static TheoryData<string, string, AnswerSpec, Difficulty, OwnerOutcome, int> Edits => new()
    {
        { "same as current", """{"stem":"raw","textNeedsReview":true}""", Key, Difficulty.OnLevel, OwnerOutcome.Approved, 1 },
        { "cleaned text", """{"stem":"clean"}""", Key, Difficulty.OnLevel, OwnerOutcome.ApprovedWithEdits, 2 },
        { "new key", """{"stem":"raw","textNeedsReview":true}""", Key with { Value = "C" }, Difficulty.OnLevel, OwnerOutcome.ApprovedWithEdits, 2 },
        { "equal key, new list instance", """{"stem":"raw","textNeedsReview":true}""", Key with { Accept = [] }, Difficulty.OnLevel, OwnerOutcome.Approved, 1 },
        { "harder", """{"stem":"raw","textNeedsReview":true}""", Key, Difficulty.Harder, OwnerOutcome.ApprovedWithEdits, 2 },
    };

    [Theory]
    [MemberData(nameof(Edits))]
    public void Approving_with_edits_bumps_the_version_only_when_something_changed(
        string _, string content, AnswerSpec answer, Difficulty difficulty, OwnerOutcome outcome, int version)
    {
        var q = AwaitingOwner();
        q.Approve(new OwnerEdits(content, answer, difficulty), "owner", Now);

        Assert.Equal(QuestionStatus.Live, q.Status);
        Assert.Equal((outcome, version), (q.OwnerDecision!.Outcome, q.Version));
        Assert.Equal((content, answer.Value, difficulty), (q.ContentJson, q.Answer.Value, q.Difficulty));
    }

    [Fact]
    public void Edits_with_bad_content_are_refused_before_anything_changes()
    {
        var q = AwaitingOwner();
        Assert.Throws<DomainException>(() => q.Approve(new OwnerEdits("not json", Key, Difficulty.OnLevel), "owner", Now));
        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
        Assert.Null(q.OwnerDecision);
    }

    [Theory]
    [InlineData("", "owner")]
    [InlineData("   ", "owner")]
    [InlineData("Choice C is wrong in the print.", "")]
    public void Rejecting_needs_a_reason_and_an_owner(string reason, string owner)
    {
        var q = AwaitingOwner();
        Assert.Throws<DomainException>(() => q.Reject(owner, Now, reason));
        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
    }

    [Fact]
    public void A_rejected_question_stays_away_from_students_with_the_reason()
    {
        var q = AwaitingOwner();
        q.Reject("owner", Now, "Figure is cut off.");

        Assert.Equal(QuestionStatus.Rejected, q.Status);
        Assert.False(q.IsVisibleToStudents);
        Assert.Equal(new OwnerDecision(OwnerOutcome.Rejected, "owner", Now, "Figure is cut off."), q.OwnerDecision);
        Assert.IsType<QuestionRejected>(Assert.Single(q.DomainEvents));
    }

    [Fact]
    public void Owners_cannot_decide_before_the_ai_reviewer_has_run()
    {
        var q = InAiReview();
        Assert.Throws<DomainException>(() => q.Approve("owner", Now));
        Assert.Throws<DomainException>(() => q.Reject("owner", Now, "no"));
    }

    [Fact]
    public void Revising_clears_old_reviews_so_the_new_version_is_reviewed_afresh()
    {
        var q = AwaitingOwner();
        q.Approve("owner", Now);
        q.Revise("""{"stem":"new"}""", Key);

        Assert.Equal(QuestionStatus.Draft, q.Status);
        Assert.Null(q.AiReview);
        Assert.Null(q.OwnerDecision);
    }
}
