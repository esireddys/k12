using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class QuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static Question NewDraft() => Question.Draft(
        [SkillId.New()], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "144" },
        Difficulty.OnLevel, QuestionOrigin.Official);

    [Fact]
    public void New_questions_start_as_drafts_hidden_from_students()
    {
        var q = NewDraft();
        Assert.Equal(QuestionStatus.Draft, q.Status);
        Assert.False(q.IsVisibleToStudents);
    }

    [Fact]
    public void A_question_goes_live_only_after_ai_review_and_owner_approval()
    {
        var q = NewDraft();
        q.SubmitForAiReview();
        q.PassAiReview();
        q.Approve("owner", Now);

        Assert.True(q.IsVisibleToStudents);
        var approved = Assert.IsType<QuestionApproved>(Assert.Single(q.DomainEvents));
        Assert.Equal(q.Id, approved.QuestionId);
    }

    [Fact]
    public void Owner_cannot_approve_a_question_the_ai_reviewer_has_not_passed()
    {
        var q = NewDraft();
        q.SubmitForAiReview();
        Assert.Throws<DomainException>(() => q.Approve("owner", Now));
    }

    [Fact]
    public void Revising_a_live_question_makes_a_new_draft_version()
    {
        var q = NewDraft();
        q.SubmitForAiReview();
        q.PassAiReview();
        q.Approve("owner", Now);

        q.Revise("{\"stem\":\"new\"}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "145" });

        Assert.Equal(2, q.Version);
        Assert.Equal(QuestionStatus.Draft, q.Status);
        Assert.False(q.IsVisibleToStudents);
    }

    [Fact]
    public void A_question_must_be_tagged_to_a_skill() =>
        Assert.Throws<DomainException>(() => Question.Draft(
            [], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" },
            Difficulty.OnLevel, QuestionOrigin.Official));
}
