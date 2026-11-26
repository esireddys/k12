using K12.Application.ContentReview;

namespace K12.Infrastructure.Content;

/// <summary>
/// Serves item pictures from a folder, for local runs: point Content:ImageRoot at the pipeline's data/out.
/// Only image files under the root are served; anything that climbs out of it is treated as missing.
/// </summary>
public sealed class FileSystemItemImageStore(string root) : IItemImageStore
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
    };

    private readonly string _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    public Task<ItemImage?> OpenAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('\\', '/').TrimStart('/')));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(_root, comparison)
            || !Types.TryGetValue(Path.GetExtension(full), out var contentType)
            || !File.Exists(full))
            return Task.FromResult<ItemImage?>(null);

        Stream stream = File.OpenRead(full);
        return Task.FromResult<ItemImage?>(new ItemImage(stream, contentType));
    }
}

/// <summary>Used when no picture folder is configured: every picture is missing.</summary>
public sealed class NoItemImageStore : IItemImageStore
{
    public Task<ItemImage?> OpenAsync(string relativePath, CancellationToken ct = default) => Task.FromResult<ItemImage?>(null);
}
