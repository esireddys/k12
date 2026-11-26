using K12.Application.Abstractions;

namespace K12.Application.ContentReview;

/// <summary>A picture of a printed item. The caller disposes the stream.</summary>
public sealed record ItemImage(Stream Content, string ContentType);

/// <summary>
/// Where item pictures live: the pipeline's output folder locally, Blob Storage in the cloud.
/// Paths are the relative ones stored in question content, for example "tx-exam-3-math-2021/07.png".
/// </summary>
public interface IItemImageStore
{
    Task<ItemImage?> OpenAsync(string relativePath, CancellationToken ct = default);
}

public sealed record GetItemImageQuery(string Path);

public sealed class GetItemImageHandler(IItemImageStore images) : IQueryHandler<GetItemImageQuery, ItemImage>
{
    public async Task<Result<ItemImage>> HandleAsync(GetItemImageQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Path)) return Error.NotFound("image.not_found", "No such picture.");
        var image = await images.OpenAsync(query.Path, ct);
        return image is null ? Error.NotFound("image.not_found", "No such picture.") : image;
    }
}
