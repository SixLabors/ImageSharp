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
        using Image<L16> image = Image.Load<L16>(TestFile.Create(WideGrayPgm).Bytes);

        Assert.Equal(0x8000, image[0, 0].PackedValue);
        Assert.Equal(0x0080, image[1, 0].PackedValue);
    }

    [Fact]
    public void Decode_WideBinaryRgb_SamplesAreBigEndian()
    {
        using Image<Rgb48> image = Image.Load<Rgb48>(TestFile.Create(WideRgbPpm).Bytes);

        Assert.Equal(new Rgb48(0x81B5, 0x8491, 0x8671), image[0, 0]);
    }

    [Fact]
    public void Decode_NonStandardByteMaxPixelValue_UpscalesToFullRange()
    {
        using Image<L8> image = Image.Load<L8>(TestFile.Create(ByteWhitePgm).Bytes);

        Assert.Equal(255, image[0, 0].PackedValue);
    }

    [Fact]
    public void Decode_NonStandardShortMaxPixelValue_UpscalesToFullRange()
    {
        using Image<L16> image = Image.Load<L16>(TestFile.Create(WideWhitePgm).Bytes);

        Assert.Equal(65535, image[0, 0].PackedValue);
    }

    /// <summary>
    /// Preserves non-standard sample ranges when packing both byte and ushort destination pixels.
    /// </summary>
    /// <param name="imagePath">The stored PGM or PPM input.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    /// <param name="isRgb">True for three-channel samples.</param>
    [Theory]
    [InlineData(P2Max100Pgm, 100, false)]
    [InlineData(P3Max100Ppm, 100, true)]
    [InlineData(P5Max100Pgm, 100, false)]
    [InlineData(P6Max100Ppm, 100, true)]
    [InlineData(P2Max1000Pgm, 1000, false)]
    [InlineData(P3Max1000Ppm, 1000, true)]
    [InlineData(P5Max1000Pgm, 1000, false)]
    [InlineData(P6Max1000Ppm, 1000, true)]
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
    [InlineData(P2AboveMax100Pgm)]
    [InlineData(P3AboveMax100Ppm)]
    [InlineData(P5AboveMax100Pgm)]
    [InlineData(P6AboveMax100Ppm)]
    [InlineData(P2AboveMax1000Pgm)]
    [InlineData(P3AboveMax1000Ppm)]
    [InlineData(P5AboveMax1000Pgm)]
    [InlineData(P6AboveMax1000Ppm)]
    [InlineData(P2DecimalOverflowPgm)]
    [InlineData(P3DecimalOverflowPpm)]
    public void Decode_SamplesAboveMaxval_SaturateInEveryIntegrityMode(string imagePath)
    {
        byte[] data = TestFile.Create(imagePath).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            using Image<Rgb48> image = Image.Load<Rgb48>(options, data);
            Assert.Equal(new Rgb48(65535, 65535, 65535), image[0, 0]);
        }
    }

    /// <summary>
    /// Keeps the available pixels and channels by default when a non-standard raster ends early.
    /// Throws when the integrity mode does not ignore image data errors.
    /// </summary>
    /// <param name="imagePath">The truncated stored input.</param>
    /// <param name="isRgb">True for a truncated RGB pixel; false for a truncated grayscale row.</param>
    [Theory]
    [InlineData(P2PartialPgm, false)]
    [InlineData(P5PartialPgm, false)]
    [InlineData(P3PartialPpm, true)]
    [InlineData(P6PartialPpm, true)]
    public void Decode_PartialRaster_PreservesAvailableImageData(string imagePath, bool isRgb)
    {
        byte[] data = TestFile.Create(imagePath).Bytes;
        Rgb24[] expected = isRgb
            ? [new(0, 2, 128), new(255, 0, 0)]
            : [new(0, 0, 0), new(18, 18, 18), new(255, 255, 255), new(128, 128, 128), default, default];

        AssertImageDataErrorThrows(data);

        // The default options ignore image data errors.
        Size size = isRgb ? new Size(2, 1) : new Size(3, 2);
        using Image<Rgb24> expectedImage = Image.LoadPixelData<Rgb24>(expected, size.Width, size.Height);
        using Image<Rgb24> image = Image.Load<Rgb24>(data);
        ImageComparer.Exact.VerifySimilarity(expectedImage, image);
    }

    /// <summary>
    /// Gets inputs with damaged or truncated full-range samples, the image size from the header, and the pixels that
    /// the default options decode. A pixel that the decoder cannot decode keeps the fill value, transparent black.
    /// </summary>
    public static TheoryData<string, int, int, Rgba32[]> DamagedSampleData { get; } = new()
    {
        // The second plain token is not a decimal number. The other samples stay decoded.
        { "P2\n3 1\n255\n10 x 30\n", 3, 1, [new(10, 10, 10), default, new(30, 30, 30)] },

        // Whitespace separates plain samples, so the damaged token "12x34" is one sample. The next token is the
        // third sample. See Netpbm pgm(5), section "Plain PGM".
        { "P2\n3 1\n255\n10 12x34 30\n", 3, 1, [new(10, 10, 10), default, new(30, 30, 30)] },

        // The damaged green token "2x" makes the whole first pixel damaged. The next token is the blue sample.
        { "P3\n2 1\n255\n1 2x 3 4 5 6\n", 2, 1, [default, new(4, 5, 6)] },

        // The plain raster ends after the red channel of the second pixel. Its missing channels are zero.
        { "P3\n2 1\n255\n1 2 3 4\n", 2, 1, [new(1, 2, 3), new(4, 0, 0)] },

        // The plain bitmap ends after two of three pixels.
        { "P1\n3 1\n1 0\n", 3, 1, [new(0, 0, 0), new(255, 255, 255), default] },

        // Each plain bitmap pixel is one byte, '0' or '1'. The byte 'x' is one damaged pixel.
        { "P1\n3 1\n1x0\n", 3, 1, [new(0, 0, 0), default, new(255, 255, 255)] },

        // Each binary bitmap row needs two bytes, but the raster has one byte. The first eight pixels stay decoded.
        {
            "P4\n9 2\n\u0080",
            9,
            2,
            [
                new(0, 0, 0), new(255, 255, 255), new(255, 255, 255), new(255, 255, 255), new(255, 255, 255),
                new(255, 255, 255), new(255, 255, 255), new(255, 255, 255), default,
                default, default, default, default, default, default, default, default, default
            ]
        },
    };

    /// <summary>
    /// Keeps the decoded samples by default when a full-range raster is damaged or ends early.
    /// Throws when the integrity mode does not ignore image data errors.
    /// </summary>
    /// <param name="text">The input. Each character is one byte.</param>
    /// <param name="width">The image width in the header.</param>
    /// <param name="height">The image height in the header.</param>
    /// <param name="expected">The pixels in row order that the default options decode.</param>
    [Theory]
    [MemberData(nameof(DamagedSampleData))]
    public void Decode_DamagedSamples_RespectsIntegrity(string text, int width, int height, Rgba32[] expected)
    {
        byte[] data = System.Text.Encoding.Latin1.GetBytes(text);

        AssertImageDataErrorThrows(data);

        // The default options ignore image data errors.
        using Image<Rgba32> expectedImage = Image.LoadPixelData<Rgba32>(expected, width, height);
        using Image<Rgba32> image = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expectedImage, image);
    }

    /// <summary>
    /// Decodes a plain bitmap whose pixels have no whitespace between them. Each pixel is one byte, '0' or '1', and
    /// whitespace in the raster is ignored. See Netpbm pbm(5), section "Plain PBM".
    /// </summary>
    [Fact]
    public void Decode_PlainBitmapWithoutWhitespace_DecodesEachDigitAsPixel()
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes("P1\n4 2\n0101\n1 1 0\n0\n");
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        // '1' is black and '0' is white.
        L8[] expected = [new(255), new(0), new(255), new(0), new(0), new(0), new(255), new(255)];
        using Image<L8> expectedImage = Image.LoadPixelData<L8>(expected, 4, 2);
        using Image<L8> image = Image.Load<L8>(options, data);
        ImageComparer.Exact.VerifySimilarity(expectedImage, image);
    }

    /// <summary>
    /// Keeps raster bytes whose values are whitespace or a comment marker.
    /// </summary>
    /// <param name="imagePath">The stored binary input.</param>
    /// <param name="isRgb">True for RGB samples.</param>
    [Theory]
    [InlineData(RasterWhitespacePgm, false)]
    [InlineData(RasterWhitespacePpm, true)]
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
        using Image<L8> image = Image.Load<L8>(TestFile.Create(RasterWhitespacePbm).Bytes);

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
        byte[] data = TestFile.Create(HeaderDecimalOverflowPgm).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
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
        DecoderOptions options = new(Configuration.Default)
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
    [InlineData(P2ZeroMaxPgm)]
    [InlineData(P3ZeroMaxPpm)]
    [InlineData(P5ZeroMaxPgm)]
    [InlineData(P6ZeroMaxPpm)]
    public void Decode_MaxPixelValueZero_ThrowsInvalidImageContentException(string imagePath)
    {
        byte[] bytes = TestFile.Create(imagePath).Bytes;
        using MemoryStream stream = new(bytes);

        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(stream));
    }

    [Fact]
    public void PlainText_PrematureEof()
    {
        using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(PlainEofPbm);

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

    /// <summary>
    /// Checks that the integrity modes that do not ignore image data errors throw for the input.
    /// </summary>
    /// <param name="data">The input with damaged or truncated image data.</param>
    private static void AssertImageDataErrorThrows(byte[] data)
    {
        foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary })
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
            });
        }
    }
}
