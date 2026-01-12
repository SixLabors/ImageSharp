// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Chunks;

namespace SixLabors.ImageSharp.Tests.Formats.WebP;

[Trait("Format", "Webp")]
public class WebpVp8XTests
{
    [Fact]
    public void WebpVp8X_WriteTo_Writes_Reserved_Bytes()
    {
        // arrange
        WebpVp8X header = new(false, false, false, false, false, 10, 40);
        MemoryStream ms = new();
        byte[] expected = [86, 80, 56, 88, 10, 0, 0, 0, 0, 0, 0, 0, 9, 0, 0, 39, 0, 0];

        // act
        header.WriteTo(ms);

        // assert
        byte[] actual = ms.ToArray();
        Assert.Equal(expected, actual);
    }
}
