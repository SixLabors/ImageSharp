// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

[Trait("Format", "Bmp")]
public class BmpFileHeaderTests
{
    [Fact]
    public void TestWrite()
    {
        BmpFileHeader header = new(1, 2, 3, 4);

        byte[] buffer = new byte[14];

        header.WriteTo(buffer);

        Assert.Equal("AQACAAAAAwAAAAQAAAA=", Convert.ToBase64String(buffer));
    }
}
