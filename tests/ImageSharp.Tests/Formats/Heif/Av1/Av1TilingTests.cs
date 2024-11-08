// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TilingTests
{
    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010E, 0x001d, 21, 1)]
    public void ReadFirstTile(string filename, int dataOffset, int dataSize, int tileOffset, int superblockCount)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, stub);
        Av1FrameBuffer frameBuffer = new(Configuration.Default, obuReader.SequenceHeader, Av1ColorFormat.Yuv444, false);
        Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        Av1FrameDecoderStub frameDecoder = new();
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder);

        // Act
        tileReader.ReadTile(tileSpan, 0);

        // Assert
        Assert.Equal(dataSize * 8, bitStreamReader.BitPosition);
        Assert.Equal(superblockCount, frameDecoder.SuperblockCount);
    }
}
