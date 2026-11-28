using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Domain.Content;

namespace K12.Application.Practice.Sets;

public sealed record GetQuestionImageQuery(Guid QuestionId);

/// <summary>The picture of a Live question, for students. Looked up by question so no other file can be asked for.</summary>
public sealed class GetQuestionImageHandler(IQuestionRepository questions, IItemImageStore images)
    : IQueryHandler<GetQuestionImageQuery, ItemImage>
{
    public async Task<Result<ItemImage>> HandleAsync(GetQuestionImageQuery query, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(query.QuestionId), ct);
        if (question is null || !question.IsVisibleToStudents) return Error.NotFound("image.not_found", "No such picture.");
        var path = QuestionContent.Read(question.ContentJson).Image;
        var image = path is null ? null : await images.OpenAsync(path, ct);
        return image is null ? Error.NotFound("image.not_found", "No such picture.") : image;
    }
}
