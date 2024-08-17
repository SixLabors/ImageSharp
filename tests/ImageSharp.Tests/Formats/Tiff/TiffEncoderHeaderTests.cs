// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Writers;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffEncoderHeaderTests
{
    private static readonly TiffEncoder Encoder = new();

    [Fact]
    public void WriteHeader_WritesValidHeader()
    {
        using MemoryStream stream = new();
        TiffEncoderCore encoder = new(Encoder, Configuration.Default);

        using (TiffStreamWriter writer = new(stream))
        {
            long firstIfdMarker = TiffEncoderCore.WriteHeader(writer, stackalloc byte[4]);
        }

        Assert.Equal(new byte[] { 0x49, 0x49, 42, 0, 0x00, 0x00, 0x00, 0x00 }, stream.ToArray());
    }

    [Fact]
    public void WriteHeader_ReturnsFirstIfdMarker()
    {
        using MemoryStream stream = new();
        TiffEncoderCore encoder = new(Encoder, Configuration.Default);

        using TiffStreamWriter writer = new(stream);
        long firstIfdMarker = TiffEncoderCore.WriteHeader(writer, stackalloc byte[4]);
        Assert.Equal(4, firstIfdMarker);
    }
}
