// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm;

[Trait("Format", "Pbm")]
[ValidateDisposedMemoryAllocations]
public class PbmDecoderTests
{
    [Theory]
    [InlineData(BlackAndWhitePlain, PbmColorType.BlackAndWhite, PbmComponentType.Bit)]
    [InlineData(BlackAndWhiteBinary, PbmColorType.BlackAndWhite, PbmComponentType.Bit)]
    [InlineData(GrayscalePlain, PbmColorType.Grayscale, PbmComponentType.Byte)]
    [InlineData(GrayscalePlainMagick, PbmColorType.Grayscale, PbmComponentType.Byte)]
    [InlineData(GrayscaleBinary, PbmColorType.Grayscale, PbmComponentType.Byte)]
    [InlineData(GrayscaleBinaryWide, PbmColorType.Grayscale, PbmComponentType.Short)]
    [InlineData(RgbPlain, PbmColorType.Rgb, PbmComponentType.Byte)]
    [InlineData(RgbPlainMagick, PbmColorType.Rgb, PbmComponentType.Byte)]
    [InlineData(RgbBinary, PbmColorType.Rgb, PbmComponentType.Byte)]
    [InlineData(RgbBinaryWide, PbmColorType.Rgb, PbmComponentType.Short)]
    public void ImageLoadCanDecode(string imagePath, PbmColorType expectedColorType, PbmComponentType expectedComponentType)
    {
        // Arrange
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // Act
        using Image image = Image.Load(stream);

        // Assert
        Assert.NotNull(image);
        PbmMetadata metadata = image.Metadata.GetPbmMetadata();
        Assert.NotNull(metadata);
        Assert.Equal(expectedColorType, metadata.ColorType);
        Assert.Equal(expectedComponentType, metadata.ComponentType);
    }

    [Theory]
    [InlineData(BlackAndWhitePlain)]
    [InlineData(BlackAndWhiteBinary)]
    [InlineData(GrayscalePlain)]
    [InlineData(GrayscalePlainMagick)]
    [InlineData(GrayscaleBinary)]
    [InlineData(GrayscaleBinaryWide)]
    public void ImageLoadL8CanDecode(string imagePath)
    {
        // Arrange
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // Act
        using Image<L8> image = Image.Load<L8>(stream);

        // Assert
        Assert.NotNull(image);
    }

    [Theory]
    [InlineData(RgbPlain)]
    [InlineData(RgbPlainMagick)]
    [InlineData(RgbBinary)]
    public void ImageLoadRgb24CanDecode(string imagePath)
    {
        // Arrange
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        // Act
        using Image<Rgb24> image = Image.Load<Rgb24>(stream);

        // Assert
        Assert.NotNull(image);
    }

    [Theory]
    [WithFile(BlackAndWhitePlain, PixelTypes.L8, "pbm")]
    [WithFile(BlackAndWhiteBinary, PixelTypes.L8, "pbm")]
    [WithFile(Issue2477, PixelTypes.L8, "pbm")]
    [WithFile(GrayscalePlain, PixelTypes.L8, "pgm")]
    [WithFile(GrayscalePlainNormalized, PixelTypes.L8, "pgm")]
    [WithFile(GrayscaleBinary, PixelTypes.L8, "pgm")]
    [WithFile(GrayscaleBinaryWide, PixelTypes.L16, "pgm")]
    [WithFile(RgbPlain, PixelTypes.Rgb24, "ppm")]
    [WithFile(RgbPlainNormalized, PixelTypes.Rgb24, "ppm")]
    [WithFile(RgbBinary, PixelTypes.Rgb24, "ppm")]
    [WithFile(RgbBinaryWide, PixelTypes.Rgb48, "ppm")]
    public void DecodeReferenceImage<TPixel>(TestImageProvider<TPixel> provider, string extension)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider, extension: extension);

        bool isGrayscale = extension is "pgm" or "pbm";
        image.CompareToReferenceOutput(provider, grayscale: isGrayscale);
    }

    [Theory]
    [WithFile(GrayscaleBinaryWide, PixelTypes.Rgb48)]
    [WithFile(RgbBinaryWide, PixelTypes.Rgb48)]
    public void Decode_WideBinary_MatchesReferenceDecoder<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PbmDecoder.Instance);
        image.CompareToOriginal(provider, ImageComparer.Exact, new MagickReferenceDecoder(PbmFormat.Instance));
    }

    [Fact]
    public void Decode_WideBinaryGrayscale_SamplesAreBigEndian()
    {
        using Image<L16> image = Image.Load<L16>(TestFile.Create(SampleDirectory + "wide-gray.pgm").Bytes);

        Assert.Equal(0x8000, image[0, 0].PackedValue);
        Assert.Equal(0x0080, image[1, 0].PackedValue);
    }

    [Fact]
    public void Decode_WideBinaryRgb_SamplesAreBigEndian()
    {
        using Image<Rgb48> image = Image.Load<Rgb48>(TestFile.Create(SampleDirectory + "wide-rgb.ppm").Bytes);

        Assert.Equal(new Rgb48(0x81B5, 0x8491, 0x8671), image[0, 0]);
    }

    [Fact]
    public void Decode_NonStandardByteMaxPixelValue_UpscalesToFullRange()
    {
        using Image<L8> image = Image.Load<L8>(TestFile.Create(SampleDirectory + "byte-white.pgm").Bytes);

        Assert.Equal(255, image[0, 0].PackedValue);
    }

    [Fact]
    public void Decode_NonStandardShortMaxPixelValue_UpscalesToFullRange()
    {
        using Image<L16> image = Image.Load<L16>(TestFile.Create(SampleDirectory + "wide-white.pgm").Bytes);

        Assert.Equal(65535, image[0, 0].PackedValue);
    }

    /// <summary>
    /// Preserves non-standard sample ranges when packing both byte and ushort destination pixels.
    /// </summary>
    /// <param name="imagePath">The stored PGM or PPM input.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    /// <param name="isRgb">True for three-channel samples.</param>
    [Theory]
    [InlineData(SampleDirectory + "p2-max100.pgm", 100, false)]
    [InlineData(SampleDirectory + "p3-max100.ppm", 100, true)]
    [InlineData(SampleDirectory + "p5-max100.pgm", 100, false)]
    [InlineData(SampleDirectory + "p6-max100.ppm", 100, true)]
    [InlineData(SampleDirectory + "p2-max1000.pgm", 1000, false)]
    [InlineData(SampleDirectory + "p3-max1000.ppm", 1000, true)]
    [InlineData(SampleDirectory + "p5-max1000.pgm", 1000, false)]
    [InlineData(SampleDirectory + "p6-max1000.ppm", 1000, true)]
    public void Decode_NonStandardMaxPixelValue_PreservesSamplePrecision(string imagePath, int maxPixelValue, bool isRgb)
    {
        // The six stored samples are 0, 7, Maxval/2, Maxval-1, Maxval and 3.
        // These fixed expectations round their fractions once at the destination precision.
        byte[] expected8 = maxPixelValue == 100 ? [0, 18, 128, 252, 255, 8] : [0, 2, 128, 255, 255, 1];
        ushort[] expected16 = maxPixelValue == 100 ? [0, 4587, 32768, 64880, 65535, 1966] : [0, 459, 32768, 65469, 65535, 197];
        byte[] data = TestFile.Create(imagePath).Bytes;
        using Image<Rgb24> image8 = Image.Load<Rgb24>(data);
        using Image<Rgb48> image16 = Image.Load<Rgb48>(data);
        int width = isRgb ? 2 : 6;
        Assert.Equal(new Size(width, 1), image8.Size);
        Assert.Equal(image8.Size, image16.Size);

        for (int x = 0; x < width; x++)
        {
            int red = isRgb ? x * 3 : x;
            int green = isRgb ? red + 1 : red;
            int blue = isRgb ? red + 2 : red;
            Assert.Equal(new Rgb24(expected8[red], expected8[green], expected8[blue]), image8[x, 0]);
            Assert.Equal(new Rgb48(expected16[red], expected16[green], expected16[blue]), image16[x, 0]);
        }
    }

    /// <summary>
    /// Saturates sample magnitudes independently of the selected integrity mode.
    /// </summary>
    /// <param name="imagePath">The stored input containing samples above Maxval.</param>
    [Theory]
    [InlineData(SampleDirectory + "p2-above-max100.pgm")]
    [InlineData(SampleDirectory + "p3-above-max100.ppm")]
    [InlineData(SampleDirectory + "p5-above-max100.pgm")]
    [InlineData(SampleDirectory + "p6-above-max100.ppm")]
    [InlineData(SampleDirectory + "p2-above-max1000.pgm")]
    [InlineData(SampleDirectory + "p3-above-max1000.ppm")]
    [InlineData(SampleDirectory + "p5-above-max1000.pgm")]
    [InlineData(SampleDirectory + "p6-above-max1000.ppm")]
    [InlineData(SampleDirectory + "p2-decimal-overflow.pgm")]
    [InlineData(SampleDirectory + "p3-decimal-overflow.ppm")]
    public void Decode_SamplesAboveMaxval_SaturateInEveryIntegrityMode(string imagePath)
    {
        byte[] data = TestFile.Create(imagePath).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            using Image<Rgb48> image = Image.Load<Rgb48>(options, data);
            Assert.Equal(new Rgb48(65535, 65535, 65535), image[0, 0]);
        }
    }

    /// <summary>
    /// Preserves the available pixels and channels when a non-standard raster ends early.
    /// </summary>
    /// <param name="imagePath">The truncated stored input.</param>
    /// <param name="isRgb">True for a truncated RGB pixel; false for a truncated grayscale row.</param>
    [Theory]
    [InlineData(SampleDirectory + "p2-partial.pgm", false)]
    [InlineData(SampleDirectory + "p5-partial.pgm", false)]
    [InlineData(SampleDirectory + "p3-partial.ppm", true)]
    [InlineData(SampleDirectory + "p6-partial.ppm", true)]
    public void Decode_PartialRaster_PreservesAvailableImageData(string imagePath, bool isRgb)
    {
        byte[] data = TestFile.Create(imagePath).Bytes;
        Rgb24[] expected = isRgb
            ? [new(0, 2, 128), new(255, 0, 0)]
            : [new(0, 0, 0), new(18, 18, 18), new(255, 255, 255), new(128, 128, 128), default, default];

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
            Assert.Equal(isRgb ? new Size(2, 1) : new Size(3, 2), image.Size);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], image[i % image.Width, i / image.Width]);
            }
        }
    }

    /// <summary>
    /// Keeps raster bytes whose values are whitespace or a comment marker.
    /// </summary>
    /// <param name="imagePath">The stored binary input.</param>
    /// <param name="isRgb">True for RGB samples.</param>
    [Theory]
    [InlineData(SampleDirectory + "raster-whitespace.pgm", false)]
    [InlineData(SampleDirectory + "raster-whitespace.ppm", true)]
    public void Decode_BinaryRaster_DoesNotSkipSampleBytes(string imagePath, bool isRgb)
    {
        // Stored magnitudes 9, 10, 13, 32 and 35 are TAB, LF, CR, space and '#'. They belong to the raster.
        byte[] expected = [23, 26, 33, 82, 89, 255];
        using Image<Rgb24> image = Image.Load<Rgb24>(TestFile.Create(imagePath).Bytes);

        for (int x = 0; x < image.Width; x++)
        {
            int red = isRgb ? x * 3 : x;
            Assert.Equal(new Rgb24(expected[red], expected[isRgb ? red + 1 : red], expected[isRgb ? red + 2 : red]), image[x, 0]);
        }
    }

    /// <summary>
    /// Keeps a binary PBM raster byte whose value equals ASCII space.
    /// </summary>
    [Fact]
    public void Decode_BinaryBitmap_DoesNotSkipRasterByte()
    {
        using Image<L8> image = Image.Load<L8>(TestFile.Create(SampleDirectory + "raster-whitespace.pbm").Bytes);

        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(x == 2 ? 0 : 255, image[x, 0].PackedValue);
        }
    }

    /// <summary>
    /// Rejects an overflowing header dimension before allocating the image.
    /// </summary>
    [Fact]
    public void Decode_OverflowingHeader_RemainsFatal()
    {
        byte[] data = TestFile.Create(SampleDirectory + "header-decimal-overflow.pgm").Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
            });
        }
    }


    [Theory]
    [WithFile(RgbPlain, PixelTypes.Rgb24)]
    public void PbmDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            TargetSize = new Size { Width = 150, Height = 150 }
        };

        using Image<TPixel> image = provider.GetImage(PbmDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Exact,
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [InlineData(SampleDirectory + "p2-zero-max.pgm")]
    [InlineData(SampleDirectory + "p3-zero-max.ppm")]
    [InlineData(SampleDirectory + "p5-zero-max.pgm")]
    [InlineData(SampleDirectory + "p6-zero-max.ppm")]
    public void Decode_MaxPixelValueZero_ThrowsInvalidImageContentException(string imagePath)
    {
        byte[] bytes = TestFile.Create(imagePath).Bytes;
        using MemoryStream stream = new(bytes);

        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(stream));
    }

    [Fact]
    public void PlainText_PrematureEof()
    {
        using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(SampleDirectory + "plain-eof.pbm");

        Assert.True(eofHitCounter.EofHitCount <= 2);
        Assert.Equal(new Size(100, 100), eofHitCounter.Image.Size);
    }

    [Fact]
    public void Binary_PrematureEof()
    {
        using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(RgbBinaryPrematureEof);

        Assert.True(eofHitCounter.EofHitCount <= 2);
        Assert.Equal(new Size(29, 30), eofHitCounter.Image.Size);
    }
}
