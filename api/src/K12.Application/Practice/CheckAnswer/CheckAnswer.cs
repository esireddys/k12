using K12.Application.Abstractions;
using K12.Domain.Content;

namespace K12.Application.Practice.CheckAnswer;

public sealed record CheckAnswerCommand(Guid QuestionId, string? Response);

/// <summary>Deliberately has no correct answer in it: in practice mode the explanation comes from its own use case.</summary>
public sealed record CheckAnswerResponse(bool IsCorrect, bool Understood);

/// <summary>Scores one response to a Live question. Recording the attempt in a session comes with the Practice aggregate.</summary>
public sealed class CheckAnswerHandler(IQuestionRepository questions) : ICommandHandler<CheckAnswerCommand, CheckAnswerResponse>
{
    public async Task<Result<CheckAnswerResponse>> HandleAsync(CheckAnswerCommand command, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(command.QuestionId), ct);
        if (question is null || !question.IsVisibleToStudents)
            return Error.NotFound("question.not_found", "No such question.");

        var result = question.Check(command.Response);
        return new CheckAnswerResponse(result.IsCorrect, result.Understood);
    }
}
