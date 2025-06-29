// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Bmp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

[Trait("Format", "Bmp")]
public class BmpEncoderTests
{
    private static BmpEncoder BmpEncoder => new();

    public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel =
        new()
        {
            BmpBitsPerPixel.Bit24,
            BmpBitsPerPixel.Bit32
        };

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { Car, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { V5Header, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
        { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
    };

    public static readonly TheoryData<string, BmpBitsPerPixel> BmpBitsPerPixelFiles =
    new()
    {
        { Bit1, BmpBitsPerPixel.Bit1 },
        { Bit2, BmpBitsPerPixel.Bit2 },
        { Bit4, BmpBitsPerPixel.Bit4 },
        { Bit8, BmpBitsPerPixel.Bit8 },
        { Rgb16, BmpBitsPerPixel.Bit16 },
        { Car, BmpBitsPerPixel.Bit24 },
        { Bit32Rgb, BmpBitsPerPixel.Bit32 }
    };

    [Fact]
    public void BmpEncoderDefaultInstanceHasQuantizer() => Assert.NotNull(BmpEncoder.Quantizer);

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, BmpEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageMetadata meta = output.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(BmpBitsPerPixelFiles))]
    public void Encode_PreserveBitsPerPixel(string imagePath, BmpBitsPerPixel bmpBitsPerPixel)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, BmpEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        BmpMetadata meta = output.Metadata.GetBmpMetadata();

        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
    }

    [Theory]
    [WithTestPatternImages(nameof(BitsPerPixel), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
    public void Encode_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

    [Theory]
    [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypes.Rgba32)]
    [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypes.Rgba32)]
    public void Encode_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    public void Encode_32Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)

        // If supportTransparency is false, a v3 bitmap header will be written.
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Bit32)]
    public void Encode_32Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(WinBmpv3, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)] // WinBmpv3 is a 24 bits per pixel image.
    [WithFile(F, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)]
    public void Encode_24Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

    [Theory]
    [WithFile(WinBmpv3, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)]
    [WithFile(F, PixelTypes.Rgb24, BmpBitsPerPixel.Bit24)]
    public void Encode_24Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    public void Encode_16Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

    [Theory]
    [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Bit16)]
    public void Encode_16Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(WinBmpv5, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    [WithFile(Bit8Palette4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    public void Encode_8Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

    [Theory]
    [WithFile(WinBmpv5, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    [WithFile(Bit8Palette4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit8)]
    public void Encode_8Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(Bit8Gs, PixelTypes.L8, BmpBitsPerPixel.Bit8)]
    public void Encode_8BitGray_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestBmpEncoderCore(
            provider,
            bitsPerPixel,
            supportTransparency: false);

    [Theory]
    [WithFile(Bit4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit4)]
    public void Encode_4Bit_WithV3Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Oddly the difference only happens locally but we'll not test for that.
        // I suspect the issue is with the reference codec.
        ImageComparer comparer = TestEnvironment.IsFramework
            ? ImageComparer.TolerantPercentage(0.0161F)
            : ImageComparer.Exact;

        TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false, customComparer: comparer);
    }

    [Theory]
    [WithFile(Bit4, PixelTypes.Rgba32, BmpBitsPerPixel.Bit4)]
    public void Encode_4Bit_WithV4Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Oddly the difference only happens locally but we'll not test for that.
        // I suspect the issue is with the reference codec.
        ImageComparer comparer = TestEnvironment.IsFramework
            ? ImageComparer.TolerantPercentage(0.0161F)
            : ImageComparer.Exact;

        TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true, customComparer: comparer);
    }

    [Theory]
    [WithFile(Bit2, PixelTypes.Rgba32, BmpBitsPerPixel.Bit2)]
    public void Encode_2Bit_WithV3Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        BmpEncoder encoder = new() { BitsPerPixel = bitsPerPixel };
        using MemoryStream memoryStream = new();
        using Image<TPixel> input = provider.GetImage(BmpDecoder.Instance);

        // act
        encoder.Encode(input, memoryStream);
        memoryStream.Position = 0;

        // assert
        using Image<TPixel> actual = Image.Load<TPixel>(memoryStream);
        ImageSimilarityReport similarityReport = ImageComparer.Exact.CompareImagesOrFrames(input, actual);
        Assert.True(similarityReport.IsEmpty, "encoded image does not match reference image");
    }

    [Theory]
    [WithFile(Bit2, PixelTypes.Rgba32, BmpBitsPerPixel.Bit2)]
    public void Encode_2Bit_WithV4Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        BmpEncoder encoder = new() { BitsPerPixel = bitsPerPixel };
        using MemoryStream memoryStream = new();
        using Image<TPixel> input = provider.GetImage(BmpDecoder.Instance);

        // act
        encoder.Encode(input, memoryStream);
        memoryStream.Position = 0;

        // assert
        using Image<TPixel> actual = Image.Load<TPixel>(memoryStream);
        ImageSimilarityReport similarityReport = ImageComparer.Exact.CompareImagesOrFrames(input, actual);
        Assert.True(similarityReport.IsEmpty, "encoded image does not match reference image");
    }

    [Theory]
    [WithFile(Bit1, PixelTypes.Rgba32, BmpBitsPerPixel.Bit1)]
    public void Encode_1Bit_WithV3Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

    [Theory]
    [WithFile(Bit1, PixelTypes.Rgba32, BmpBitsPerPixel.Bit1)]
    public void Encode_1Bit_WithV4Header_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(Bit8Gs, PixelTypes.L8, BmpBitsPerPixel.Bit8)]
    public void Encode_8BitGray_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestBmpEncoderCore(
            provider,
            bitsPerPixel,
            supportTransparency: true);

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    public void Encode_8BitColor_WithWuQuantizer<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit8,
            Quantizer = new WuQuantizer()
        };

        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

        // Use the default decoder to test our encoded image. This verifies the content.
        // We do not verify the reference image though as some are invalid.
        IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
        using FileStream stream = File.OpenRead(actualOutputFile);
        using Image<TPixel> referenceImage = referenceDecoder.Decode<TPixel>(DecoderOptions.Default, stream);
        referenceImage.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            extension: "bmp",
            appendPixelTypeToFileName: false,
            decoder: new MagickReferenceDecoder(BmpFormat.Instance, false));
    }

    [Theory]
    [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
    public void Encode_8BitColor_WithOctreeQuantizer<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit8,
            Quantizer = new OctreeQuantizer()
        };
        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

        // Use the default decoder to test our encoded image. This verifies the content.
        // We do not verify the reference image though as some are invalid.
        IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
        using FileStream stream = File.OpenRead(actualOutputFile);
        using Image<TPixel> referenceImage = referenceDecoder.Decode<TPixel>(DecoderOptions.Default, stream);
        referenceImage.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            extension: "bmp",
            appendPixelTypeToFileName: false,
            decoder: new MagickReferenceDecoder(BmpFormat.Instance, false));
    }

    [Theory]
    [WithFile(TestImages.Png.GrayAlpha2BitInterlaced, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    [WithFile(Bit32Rgba, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    public void Encode_PreservesAlpha<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

    [Theory]
    [WithFile(IccProfile, PixelTypes.Rgba32)]
    public void Encode_PreservesColorProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> input = provider.GetImage(BmpDecoder.Instance, new BmpDecoderOptions());
        ImageSharp.Metadata.Profiles.Icc.IccProfile expectedProfile = input.Metadata.IccProfile;
        byte[] expectedProfileBytes = expectedProfile.ToByteArray();

        using MemoryStream memStream = new();
        input.Save(memStream, new BmpEncoder());

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageSharp.Metadata.Profiles.Icc.IccProfile actualProfile = output.Metadata.IccProfile;
        byte[] actualProfileBytes = actualProfile.ToByteArray();

        Assert.NotNull(actualProfile);
        Assert.Equal(expectedProfileBytes, actualProfileBytes);
    }

    [Theory]
    [InlineData(1, 66535)]
    [InlineData(66535, 1)]
    public void Encode_WorksWithSizeGreaterThen65k(int width, int height)
    {
        Exception exception = Record.Exception(() =>
        {
            using Image image = new Image<Rgba32>(width, height);
            using MemoryStream memStream = new();
            image.Save(memStream, BmpEncoder);
        });

        Assert.Null(exception);
    }

    [Theory]
    [WithFile(Car, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    [WithFile(V5Header, PixelTypes.Rgba32, BmpBitsPerPixel.Bit32)]
    public void Encode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InBytesSqrt(100);
        TestBmpEncoderCore(provider, bitsPerPixel);
    }

    [Theory]
    [WithFile(BlackWhitePalletDataMatrix, PixelTypes.Rgb24, BmpBitsPerPixel.Bit1)]
    public void Encode_Issue2467<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        using MemoryStream reencodedStream = new();
        BmpEncoder encoder = new()
        {
            BitsPerPixel = bitsPerPixel,
            SupportTransparency = false,
            Quantizer = KnownQuantizers.Octree
        };
        image.SaveAsBmp(reencodedStream, encoder);
        reencodedStream.Seek(0, SeekOrigin.Begin);

        using Image<TPixel> reencodedImage = Image.Load<TPixel>(reencodedStream);

        reencodedImage.DebugSave(provider);

        reencodedImage.CompareToOriginal(provider);
    }

    [Fact]
    public void Encode_WithTransparentColorBehaviorClear_Works()
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        BmpEncoder encoder = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Bit32,
            SupportTransparency = true,
            TransparentColorMode = TransparentColorMode.Clear,
        };
        Rgba32 rgba32 = Color.Blue.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                // Half of the test image should be transparent.
                if (y > 25)
                {
                    rgba32.A = 0;
                }

                for (int x = 0; x < image.Width; x++)
                {
                    rowSpan[x] = Rgba32.FromRgba32(rgba32);
                }
            }
        });

        // act
        using MemoryStream memStream = new();
        image.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(memStream);
        Rgba32 expectedColor = Color.Blue.ToPixel<Rgba32>();

        actual.ProcessPixelRows(accessor =>
        {
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }

    private static void TestBmpEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        BmpBitsPerPixel bitsPerPixel,
        bool supportTransparency = true, // if set to true, will write a V4 header, otherwise a V3 header.
        IQuantizer quantizer = null,
        ImageComparer customComparer = null,
        IImageDecoder referenceDecoder = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        // There is no alpha in bmp with less then 32 bits per pixels, so the reference image will be made opaque.
        if (bitsPerPixel != BmpBitsPerPixel.Bit32)
        {
            image.Mutate(c => c.MakeOpaque());
        }

        BmpEncoder encoder = new()
        {
            BitsPerPixel = bitsPerPixel,
            SupportTransparency = supportTransparency,
            Quantizer = quantizer ?? KnownQuantizers.Octree
        };

        // Does DebugSave & load reference CompareToReferenceInput():
        image.VerifyEncoder(provider, "bmp", bitsPerPixel, encoder, customComparer);
    }
}
