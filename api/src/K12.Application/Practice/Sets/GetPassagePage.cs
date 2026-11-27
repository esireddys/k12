using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Domain.Content;

namespace K12.Application.Practice.Sets;

/// <param name="Page">1 for the first printed page.</param>
public sealed record GetPassagePageQuery(Guid PassageId, int Page);

/// <summary>
/// One printed page of a reading selection, for students. Looked up by passage and page number so no other file
/// can be asked for. A passage from a released test is public with its test; any other passage is shown only once
/// a Live question is about it.
/// </summary>
public sealed class GetPassagePageHandler(IPassageRepository passages, IQuestionRepository questions, IItemImageStore images)
    : IQueryHandler<GetPassagePageQuery, ItemImage>
{
    private static readonly Error NotFound = Error.NotFound("image.not_found", "No such picture.");

    public async Task<Result<ItemImage>> HandleAsync(GetPassagePageQuery query, CancellationToken ct = default)
    {
        var passage = await passages.GetAsync(new PassageId(query.PassageId), ct);
        if (passage is null) return NotFound;
        if (passage.Origin is not QuestionOrigin.Official)
        {
            var live = await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct);
            if (!live.Any(q => q.PassageId == passage.Id)) return NotFound;
        }

        var pages = PassageContent.Read(passage.ContentJson).Pages;
        if (query.Page < 1 || query.Page > pages.Count) return NotFound;
        var image = await images.OpenAsync(pages[query.Page - 1], ct);
        return image is null ? NotFound : image;
    }
}
