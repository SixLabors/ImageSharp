// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tga;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Tga;

[Trait("Format", "Tga")]
public class TgaEncoderTests
{
    public static readonly TheoryData<TgaBitsPerPixel> BitsPerPixel =
        new()
        {
            TgaBitsPerPixel.Bit24,
            TgaBitsPerPixel.Bit32
        };

    public static readonly TheoryData<string, TgaBitsPerPixel> TgaBitsPerPixelFiles =
        new()
        {
            { Gray8BitBottomLeft, TgaBitsPerPixel.Bit8 },
            { Bit16BottomLeft, TgaBitsPerPixel.Bit16 },
            { Bit24BottomLeft, TgaBitsPerPixel.Bit24 },
            { Bit32BottomLeft, TgaBitsPerPixel.Bit32 },
        };

    [Theory]
    [MemberData(nameof(TgaBitsPerPixelFiles))]
    public void TgaEncoder_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
    {
        var options = new TgaEncoder();

        var testFile = TestFile.Create(imagePath);
        using (Image<Rgba32> input = testFile.CreateRgba32Image())
        {
            using (var memStream = new MemoryStream())
            {
                input.Save(memStream, options);
                memStream.Position = 0;
                using (var output = Image.Load<Rgba32>(memStream))
                {
                    TgaMetadata meta = output.Metadata.GetTgaMetadata();
                    Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TgaBitsPerPixelFiles))]
    public void TgaEncoder_WithCompression_PreserveBitsPerPixel(string imagePath, TgaBitsPerPixel bmpBitsPerPixel)
    {
        var options = new TgaEncoder() { Compression = TgaCompression.RunLength };
        var testFile = TestFile.Create(imagePath);
        using (Image<Rgba32> input = testFile.CreateRgba32Image())
        {
            using (var memStream = new MemoryStream())
            {
                input.Save(memStream, options);
                memStream.Position = 0;
                using (var output = Image.Load<Rgba32>(memStream))
                {
                    TgaMetadata meta = output.Metadata.GetTgaMetadata();
                    Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                }
            }
        }
    }

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit8_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit8)

        // Using tolerant comparer here. The results from magick differ slightly. Maybe a different ToGrey method is used. The image looks otherwise ok.
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None, useExactComparer: false, compareTolerance: 0.03f);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit16_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit16)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None, useExactComparer: false);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit24_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit24)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit32_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit32)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.None);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit8_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit8)

        // Using tolerant comparer here. The results from magick differ slightly. Maybe a different ToGrey method is used. The image looks otherwise ok.
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength, useExactComparer: false, compareTolerance: 0.03f);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit16_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit16)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength, useExactComparer: false);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit24_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit24)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32)]
    public void TgaEncoder_Bit32_WithRunLengthEncoding_Works<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel = TgaBitsPerPixel.Bit32)
        where TPixel : unmanaged, IPixel<TPixel> => TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);

    [Theory]
    [WithFile(WhiteStripesPattern, PixelTypes.Rgba32, 2748)]
    public void TgaEncoder_DoesNotAlwaysUseRunLengthPackets<TPixel>(TestImageProvider<TPixel> provider, int expectedBytes)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The test image has alternating black and white pixels, which should make using always RLE data inefficient.
        using Image<TPixel> image = provider.GetImage();
        TgaEncoder options = new() { BitsPerPixel = TgaBitsPerPixel.Bit24, Compression = TgaCompression.RunLength };

        using MemoryStream memStream = new();
        image.Save(memStream, options);
        byte[] imageBytes = memStream.ToArray();

        Assert.Equal(expectedBytes, imageBytes.Length);
    }

    // Run length encoded pixels should not exceed row boundaries.
    // https://github.com/SixLabors/ImageSharp/pull/2172
    [Fact]
    public void TgaEncoder_RunLengthDoesNotCrossRowBoundaries()
    {
        var options = new TgaEncoder() { Compression = TgaCompression.RunLength };

        using (var input = new Image<Rgba32>(30, 30))
        {
            using (var memStream = new MemoryStream())
            {
                input.Save(memStream, options);
                byte[] imageBytes = memStream.ToArray();
                Assert.Equal(138, imageBytes.Length);
            }
        }
    }

    [Theory]
    [WithFile(Bit32BottomLeft, PixelTypes.Rgba32, TgaBitsPerPixel.Bit32)]
    [WithFile(Bit24BottomLeft, PixelTypes.Rgba32, TgaBitsPerPixel.Bit24)]
    public void TgaEncoder_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, TgaBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);
        TestTgaEncoderCore(provider, bitsPerPixel, TgaCompression.RunLength);
    }

    private static void TestTgaEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        TgaBitsPerPixel bitsPerPixel,
        TgaCompression compression = TgaCompression.None,
        bool useExactComparer = true,
        float compareTolerance = 0.01f)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            var encoder = new TgaEncoder { BitsPerPixel = bitsPerPixel, Compression = compression };

            using (var memStream = new MemoryStream())
            {
                image.DebugSave(provider, encoder);
                image.Save(memStream, encoder);
                memStream.Position = 0;
                using (var encodedImage = (Image<TPixel>)Image.Load(memStream))
                {
                    ImageComparingUtils.CompareWithReferenceDecoder(provider, encodedImage, useExactComparer, compareTolerance);
                }
            }
        }
    }
}
