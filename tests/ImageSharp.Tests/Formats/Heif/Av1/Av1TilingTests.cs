// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TilingTests
{
    // [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 0x03CC - 18)]
    public void ReadFirstTile(string filename, int headerOffset, int headerSize, int tileOffset, int tileSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> headerSpan = content.AsSpan(headerOffset, headerSize);
        Span<byte> tileSpan = content.AsSpan(tileOffset, tileSize);
        Av1BitStreamReader reader = new(headerSpan);
        IAv1TileDecoder stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.Read(ref reader, headerSize, stub);
        Av1TileDecoder decoder = new(obuReader.SequenceHeader, obuReader.FrameHeader);

        // Act
        decoder.DecodeTile(tileSpan, 0);

        // Assert
    }
}
