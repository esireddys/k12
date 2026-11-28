using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;

namespace K12.Application.ContentImport.PublishReleased;

/// <summary>Publishes every released question that an older import left waiting in review.</summary>
public sealed record PublishReleasedQuestionsCommand;

public sealed record PublishReleasedQuestionsResponse(int QuestionsPublished);

/// <summary>
/// Questions from released tests go Live without AI or owner review. Imports made before that rule left them
/// in review; this moves them Live. Rejected and retired questions are left alone. Safe to run again: the second
/// run finds nothing to publish.
/// </summary>
public sealed class PublishReleasedQuestionsHandler(IQuestionRepository questions, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<PublishReleasedQuestionsCommand, PublishReleasedQuestionsResponse>
{
    public async Task<Result<PublishReleasedQuestionsResponse>> HandleAsync(
        PublishReleasedQuestionsCommand command, CancellationToken ct = default)
    {
        var waiting = await questions.ListByOriginAsync(QuestionOrigin.Official, Question.PublishableReleasedStatuses, ct);
        if (waiting.Count == 0) return new PublishReleasedQuestionsResponse(0);

        var now = clock.UtcNow;
        foreach (var question in waiting) question.PublishReleased(now);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Error.Conflict("import.publish_conflict", "Some questions changed while publishing. Run it again.");
        }

        return new PublishReleasedQuestionsResponse(waiting.Count);
    }
}
