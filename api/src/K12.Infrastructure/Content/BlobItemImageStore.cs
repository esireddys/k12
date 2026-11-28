using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using K12.Application.ContentReview;

namespace K12.Infrastructure.Content;

/// <summary>
/// Serves item pictures from a private Blob Storage container, in the cloud. The content pipeline uploads its data/out
/// folder there, so blob names are the same relative paths that question content stores ("tx-staar-g3-math-2021/07.png").
/// The API reads with its managed identity; the container is never public.
/// </summary>
public sealed class BlobItemImageStore(BlobContainerClient container) : IItemImageStore
{
    public BlobItemImageStore(Uri containerUri, TokenCredential credential) : this(new BlobContainerClient(containerUri, credential))
    {
    }

    public async Task<ItemImage?> OpenAsync(string relativePath, CancellationToken ct = default)
    {
        if (!TryBlobName(relativePath, out var name, out var contentType)) return null;
        try
        {
            var download = await container.GetBlobClient(name).OpenReadAsync(cancellationToken: ct);
            return new ItemImage(download, contentType);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// The blob name for a stored picture path, or false when the path is not a picture under the container root:
    /// backslashes become slashes, and any empty, "." or ".." segment is refused rather than resolved.
    /// </summary>
    internal static bool TryBlobName(string relativePath, out string name, out string contentType)
    {
        name = string.Empty;
        contentType = string.Empty;
        var segments = relativePath.Replace('\\', '/').TrimStart('/').Split('/');
        if (segments.Any(s => s is "" or "." or "..")) return false;
        if (!ItemImageTypes.TryGet(segments[^1], out contentType)) return false;
        name = string.Join('/', segments);
        return true;
    }
}

/// <summary>The picture formats the stores serve, by file extension.</summary>
internal static class ItemImageTypes
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
    };

    public static bool TryGet(string fileName, out string contentType) =>
        Types.TryGetValue(Path.GetExtension(fileName), out contentType!);
}
