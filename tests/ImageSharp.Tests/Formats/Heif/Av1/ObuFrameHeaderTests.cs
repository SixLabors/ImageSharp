// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class ObuFrameHeaderTests
{
    [Theory]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0102, 0x000D, false)]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0198, 0x6BD1, false)]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x0017, false)]
    public void ReadFrameHeader(string filename, int fileOffset, int blockSize, bool isAnnexB)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Av1BitStreamReader reader = new(content.AsSpan(fileOffset));
        Av1DecoderHandle decoder = new();

        // Act
        ObuReader.Read(ref reader, blockSize, decoder, isAnnexB);

        // Assert
        Assert.True(decoder.SequenceHeaderDone);
        Assert.False(decoder.SeenFrameHeader);
    }

}
