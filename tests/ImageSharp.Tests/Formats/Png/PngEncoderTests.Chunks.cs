// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public partial class PngEncoderTests
{
    [Fact]
    public void HeaderChunk_ComesFirst()
    {
        // arrange
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, PngEncoder);

        // assert
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
        PngChunkType type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
        Assert.Equal(PngChunkType.Header, type);
    }

    [Fact]
    public void EndChunk_IsLast()
    {
        // arrange
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, PngEncoder);

        // assert
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        bool endChunkFound = false;
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            Assert.False(endChunkFound);
            if (type == PngChunkType.End)
            {
                endChunkFound = true;
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }
    }

    [Theory]
    [WithFile(TestImages.Png.DefaultNotAnimated, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32)]
    public void AcTL_CorrectlyWritten<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata metadata = image.Metadata.GetPngMetadata();
        int correctFrameCount = image.Frames.Count - (metadata.AnimateRootFrame ? 0 : 1);
        using MemoryStream memStream = new();
        image.Save(memStream, PngEncoder);
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        bool foundAcTl = false;
        while (bytesSpan.Length > 0 && !foundAcTl)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            if (type == PngChunkType.AnimationControl)
            {
                AnimationControl control = AnimationControl.Parse(bytesSpan[8..]);
                foundAcTl = true;
                Assert.True(control.NumberFrames == correctFrameCount);
                Assert.True(control.NumberPlays == metadata.RepeatCount);
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }

        Assert.True(foundAcTl);
    }

    [Theory]
    [InlineData(PngChunkType.Gamma)]
    [InlineData(PngChunkType.Chroma)]
    [InlineData(PngChunkType.EmbeddedColorProfile)]
    [InlineData(PngChunkType.SignificantBits)]
    [InlineData(PngChunkType.StandardRgbColourSpace)]
    public void Chunk_ComesBeforePlteAndIDat(object chunkTypeObj)
    {
        // arrange
        PngChunkType chunkType = (PngChunkType)chunkTypeObj;
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, PngEncoder);

        // assert
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        bool palFound = false;
        bool dataFound = false;
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            if (chunkType == type)
            {
                Assert.False(palFound || dataFound, $"{chunkType} chunk should come before data and palette chunk");
            }

            switch (type)
            {
                case PngChunkType.Data:
                    dataFound = true;
                    break;
                case PngChunkType.Palette:
                    palFound = true;
                    break;
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }
    }

    [Theory]
    [InlineData(PngChunkType.Physical)]
    [InlineData(PngChunkType.SuggestedPalette)]
    public void Chunk_ComesBeforeIDat(object chunkTypeObj)
    {
        // arrange
        PngChunkType chunkType = (PngChunkType)chunkTypeObj;
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, PngEncoder);

        // assert
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        bool dataFound = false;
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType type = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            if (chunkType == type)
            {
                Assert.False(dataFound, $"{chunkType} chunk should come before data chunk");
            }

            if (type == PngChunkType.Data)
            {
                dataFound = true;
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }
    }

    [Fact]
    public void IgnoreMetadata_WillExcludeAllAncillaryChunks()
    {
        // arrange
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        PngEncoder encoder = new() { SkipMetadata = true, TextCompressionThreshold = 8 };
        Dictionary<PngChunkType, bool> expectedChunkTypes = new()
        {
            { PngChunkType.Header, false },
            { PngChunkType.Palette, false },
            { PngChunkType.Data, false },
            { PngChunkType.End, false }
        };
        List<PngChunkType> excludedChunkTypes =
        [
            PngChunkType.Gamma,
            PngChunkType.Exif,
            PngChunkType.Physical,
            PngChunkType.Text,
            PngChunkType.InternationalText,
            PngChunkType.CompressedText
        ];

        // act
        input.Save(memStream, encoder);

        // assert
        Assert.True(excludedChunkTypes.Count > 0);
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            Assert.False(excludedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been excluded");
            if (expectedChunkTypes.ContainsKey(chunkType))
            {
                expectedChunkTypes[chunkType] = true;
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }

        // all expected chunk types should have been seen at least once.
        foreach (PngChunkType chunkType in expectedChunkTypes.Keys)
        {
            Assert.True(expectedChunkTypes[chunkType], $"We expect {chunkType} chunk to be present at least once");
        }
    }

    [Theory]
    [InlineData(PngChunkFilter.ExcludeGammaChunk)]
    [InlineData(PngChunkFilter.ExcludeExifChunk)]
    [InlineData(PngChunkFilter.ExcludePhysicalChunk)]
    [InlineData(PngChunkFilter.ExcludeTextChunks)]
    [InlineData(PngChunkFilter.ExcludeAll)]
    public void ExcludeFilter_Works(object filterObj)
    {
        // arrange
        PngChunkFilter chunkFilter = (PngChunkFilter)filterObj;
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        PngEncoder encoder = new() { ChunkFilter = chunkFilter, TextCompressionThreshold = 8 };
        Dictionary<PngChunkType, bool> expectedChunkTypes = new()
        {
            { PngChunkType.Header, false },
            { PngChunkType.Gamma, false },
            { PngChunkType.Palette, false },
            { PngChunkType.InternationalText, false },
            { PngChunkType.Text, false },
            { PngChunkType.CompressedText, false },
            { PngChunkType.Exif, false },
            { PngChunkType.Physical, false },
            { PngChunkType.Data, false },
            { PngChunkType.End, false }
        };
        List<PngChunkType> excludedChunkTypes = [];
        switch (chunkFilter)
        {
            case PngChunkFilter.ExcludeGammaChunk:
                excludedChunkTypes.Add(PngChunkType.Gamma);
                expectedChunkTypes.Remove(PngChunkType.Gamma);
                break;
            case PngChunkFilter.ExcludeExifChunk:
                excludedChunkTypes.Add(PngChunkType.Exif);
                expectedChunkTypes.Remove(PngChunkType.Exif);
                break;
            case PngChunkFilter.ExcludePhysicalChunk:
                excludedChunkTypes.Add(PngChunkType.Physical);
                expectedChunkTypes.Remove(PngChunkType.Physical);
                break;
            case PngChunkFilter.ExcludeTextChunks:
                excludedChunkTypes.Add(PngChunkType.Text);
                excludedChunkTypes.Add(PngChunkType.InternationalText);
                excludedChunkTypes.Add(PngChunkType.CompressedText);
                expectedChunkTypes.Remove(PngChunkType.Text);
                expectedChunkTypes.Remove(PngChunkType.InternationalText);
                expectedChunkTypes.Remove(PngChunkType.CompressedText);
                break;
            case PngChunkFilter.ExcludeAll:
                excludedChunkTypes.Add(PngChunkType.Gamma);
                excludedChunkTypes.Add(PngChunkType.Exif);
                excludedChunkTypes.Add(PngChunkType.Physical);
                excludedChunkTypes.Add(PngChunkType.Text);
                excludedChunkTypes.Add(PngChunkType.InternationalText);
                excludedChunkTypes.Add(PngChunkType.CompressedText);
                expectedChunkTypes.Remove(PngChunkType.Gamma);
                expectedChunkTypes.Remove(PngChunkType.Exif);
                expectedChunkTypes.Remove(PngChunkType.Physical);
                expectedChunkTypes.Remove(PngChunkType.Text);
                expectedChunkTypes.Remove(PngChunkType.InternationalText);
                expectedChunkTypes.Remove(PngChunkType.CompressedText);
                break;
        }

        // act
        input.Save(memStream, encoder);

        // assert
        Assert.True(excludedChunkTypes.Count > 0);
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            Assert.False(excludedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been excluded");
            if (expectedChunkTypes.ContainsKey(chunkType))
            {
                expectedChunkTypes[chunkType] = true;
            }

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }

        // all expected chunk types should have been seen at least once.
        foreach (PngChunkType chunkType in expectedChunkTypes.Keys)
        {
            Assert.True(expectedChunkTypes[chunkType], $"We expect {chunkType} chunk to be present at least once");
        }
    }

    [Fact]
    public void ExcludeFilter_WithNone_DoesNotExcludeChunks()
    {
        // arrange
        TestFile testFile = TestFile.Create(TestImages.Png.PngWithMetadata);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        PngEncoder encoder = new() { ChunkFilter = PngChunkFilter.None, TextCompressionThreshold = 8 };
        List<PngChunkType> expectedChunkTypes =
        [
            PngChunkType.Header,
            PngChunkType.Gamma,
            PngChunkType.EmbeddedColorProfile,
            PngChunkType.Palette,
            PngChunkType.InternationalText,
            PngChunkType.Text,
            PngChunkType.CompressedText,
            PngChunkType.Exif,
            PngChunkType.Physical,
            PngChunkType.Data,
            PngChunkType.End
        ];

        // act
        input.Save(memStream, encoder);
        memStream.Position = 0;
        Span<byte> bytesSpan = memStream.ToArray().AsSpan(8); // Skip header.
        while (bytesSpan.Length > 0)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytesSpan[..4]);
            PngChunkType chunkType = (PngChunkType)BinaryPrimitives.ReadInt32BigEndian(bytesSpan.Slice(4, 4));
            Assert.True(expectedChunkTypes.Contains(chunkType), $"{chunkType} chunk should have been present");

            bytesSpan = bytesSpan[(4 + 4 + length + 4)..];
        }
    }
}
