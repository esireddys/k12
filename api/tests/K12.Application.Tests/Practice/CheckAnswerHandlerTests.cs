using K12.Application.Abstractions;
using K12.Application.Practice.CheckAnswer;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.Practice;

public class CheckAnswerHandlerTests
{
    private sealed class FakeQuestions : IQuestionRepository
    {
        private readonly Dictionary<QuestionId, Question> _items = [];
        public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task AddAsync(Question question, CancellationToken ct = default) { _items[question.Id] = question; return Task.CompletedTask; }
        public Task<IReadOnlyList<Question>> ListByOriginAsync(
            QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(_items.Values.Where(q => q.Origin == origin && statuses.Contains(q.Status)).ToList());
        public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(_items.Values.Where(q => q.Status == status).Take(take).ToList());
        public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(_items.Values.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));
    }

    private static Question Live()
    {
        var q = Question.Draft([SkillId.New()], "numeric", "{}",
            new AnswerSpec { Kind = AnswerKind.Fraction, Value = "3/4" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        q.SubmitForAiReview();
        q.RecordAiReview(TestReviews.LooksGood());
        q.Approve("owner", DateTimeOffset.UnixEpoch);
        return q;
    }

    [Fact]
    public async Task Scores_a_response_to_a_live_question()
    {
        var repo = new FakeQuestions();
        var question = Live();
        await repo.AddAsync(question);

        var result = await new CheckAnswerHandler(repo).HandleAsync(new CheckAnswerCommand(question.Id.Value, "6/8"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsCorrect);
    }

    [Fact]
    public async Task Draft_questions_are_not_found_for_students()
    {
        var repo = new FakeQuestions();
        var draft = Question.Draft([SkillId.New()], "numeric", "{}",
            new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        await repo.AddAsync(draft);

        var result = await new CheckAnswerHandler(repo).HandleAsync(new CheckAnswerCommand(draft.Id.Value, "1"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Unknown_questions_are_not_found()
    {
        var result = await new CheckAnswerHandler(new FakeQuestions()).HandleAsync(new CheckAnswerCommand(Guid.NewGuid(), "1"));
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }
}
