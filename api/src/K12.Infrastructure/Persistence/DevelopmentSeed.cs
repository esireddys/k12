using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Infrastructure.Persistence;

/// <summary>One made-up Live question for local development. Not a real released test item.</summary>
public static class DevelopmentSeed
{
    public static async Task<Question> AddSampleQuestionAsync(IQuestionRepository repository, DateTimeOffset now)
    {
        var question = Question.Draft(
            [SkillId.New()],
            itemType: "numeric",
            contentJson: """{"stem":"A school has 6 classes. Each class has 24 students. How many students are in the school?"}""",
            answer: new AnswerSpec { Kind = AnswerKind.Integer, Value = "144" },
            difficulty: Difficulty.OnLevel,
            origin: QuestionOrigin.Generated);
        question.SubmitForAiReview();
        question.PassAiReview();
        question.Approve("development-seed", now);
        question.ClearDomainEvents();
        await repository.AddAsync(question);
        return question;
    }
}
