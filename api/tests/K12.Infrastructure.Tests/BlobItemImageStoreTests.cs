using K12.Infrastructure.Content;

namespace K12.Infrastructure.Tests;

public class BlobItemImageStoreTests
{
    [Theory]
    [InlineData("tx-staar-g3-math-2021/07.png", "tx-staar-g3-math-2021/07.png", "image/png")]
    [InlineData("/tx-staar-g3-math-2021/07.PNG", "tx-staar-g3-math-2021/07.PNG", "image/png")]
    [InlineData(@"tx-staar-g3-reading-2022\p1-2.jpg", "tx-staar-g3-reading-2022/p1-2.jpg", "image/jpeg")]
    [InlineData("form/01.webp", "form/01.webp", "image/webp")]
    [InlineData("../secrets/01.png", null, null)]
    [InlineData("form/../../01.png", null, null)]
    [InlineData("form/./01.png", null, null)]
    [InlineData("form//01.png", null, null)]
    [InlineData("form/01.json", null, null)]
    [InlineData("form/", null, null)]
    [InlineData("", null, null)]
    public void Only_picture_paths_inside_the_container_become_blob_names(string path, string? name, string? contentType)
    {
        var ok = BlobItemImageStore.TryBlobName(path, out var blobName, out var type);
        Assert.Equal(name is not null, ok);
        if (ok) Assert.Equal((name, contentType), (blobName, type));
    }
}
