// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public partial class JpegEncoderTests
{
    private static JpegEncoder JpegEncoder => new();

    private static readonly TheoryData<int> TestQualities = new()
    {
        40,
        80,
        100,
    };

    public static readonly TheoryData<JpegColorType, int, float> NonSubsampledEncodingSetups = new()
    {
        { JpegColorType.Rgb, 100, 0.0238f / 100 },
        { JpegColorType.Rgb, 80,  1.3044f / 100 },
        { JpegColorType.Rgb, 40,  2.9879f / 100 },
        { JpegColorType.YCbCrRatio444, 100, 0.0780f / 100 },
        { JpegColorType.YCbCrRatio444, 80,  1.4585f / 100 },
        { JpegColorType.YCbCrRatio444, 40,  3.1413f / 100 },
    };

    public static readonly TheoryData<JpegColorType, int, float> SubsampledEncodingSetups = new()
    {
        { JpegColorType.YCbCrRatio422, 100, 0.4895f / 100 },
        { JpegColorType.YCbCrRatio422, 80,  1.6043f / 100 },
        { JpegColorType.YCbCrRatio422, 40,  3.1996f / 100 },
        { JpegColorType.YCbCrRatio420, 100, 0.5790f / 100 },
        { JpegColorType.YCbCrRatio420, 80,  1.6692f / 100 },
        { JpegColorType.YCbCrRatio420, 40,  3.2324f / 100 },
        { JpegColorType.YCbCrRatio411, 100, 0.6868f / 100 },
        { JpegColorType.YCbCrRatio411, 80,  1.7139f / 100 },
        { JpegColorType.YCbCrRatio411, 40,  3.2634f / 100 },
        { JpegColorType.YCbCrRatio410, 100, 0.7357f / 100 },
        { JpegColorType.YCbCrRatio410, 80,  1.7495f / 100 },
        { JpegColorType.YCbCrRatio410, 40,  3.2911f / 100 },
    };

    // These fixtures retain their CMYK profiles. The limits include ICC return loss and JPEG compression loss.
    // External check, 15 October 2026: LittleCMS 2.19 converted the same sRGB input pixels to their original profiles,
    // then libjpeg-turbo 3.1.91 encoded and decoded CMYK/YCCK at qualities 100, 80 and 40. LittleCMS returned RGB.
    // Both ICC directions used relative colorimetric intent without black-point compensation. All four JPEG components
    // used 1x1 sampling and luminance quantization table 0, as this encoder does. LibTIFF 4.7.2 stored RGB without conversion.
    // Native image differences from the input, using this comparer's metric, were:
    //                  Q100        Q80         Q40
    // CMYK             0.11401%    0.39494%    0.65777%
    // YCCK             0.21533%    0.24389%    0.33217%
    // Round these independent measurements up to the next 0.01 percentage point for the limits below.
    // The former limits predate ICC conversion. Without JPEG, both ICC implementations already change the CMYK fixture's
    // RGB (3,35,45) to (21,39,46), and the YCCK fixture's RGB (9,167,86) to (23,167,85).
    public static readonly TheoryData<JpegColorType, int, float> CmykEncodingSetups = new()
    {
        { JpegColorType.Cmyk, 100, 0.12f / 100 },
        { JpegColorType.Cmyk, 80,  0.40f / 100 },
        { JpegColorType.Cmyk, 40,  0.66f / 100 },
    };

    public static readonly TheoryData<JpegColorType, int, float> YcckEncodingSetups = new()
    {
        { JpegColorType.Ycck, 100, 0.22f / 100 },
        { JpegColorType.Ycck, 80,  0.25f / 100 },
        { JpegColorType.Ycck, 40,  0.34f / 100 },
    };

    public static readonly TheoryData<JpegColorType, int, float> LuminanceEncodingSetups = new()
    {
        { JpegColorType.Luminance, 100, 0.0175f / 100 },
        { JpegColorType.Luminance, 80,  0.6730f / 100 },
        { JpegColorType.Luminance, 40, 0.9943f / 100 },
    };

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(NonSubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(SubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.BikeGrayscale, nameof(LuminanceEncodingSetups), PixelTypes.L8)]
    [WithFile(TestImages.Jpeg.Baseline.Cmyk, nameof(CmykEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, nameof(YcckEncodingSetups), PixelTypes.Rgb24)]
    public void EncodeBaseline_Interleaved<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, tolerance);

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(NonSubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(SubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.BikeGrayscale, nameof(LuminanceEncodingSetups), PixelTypes.L8)]
    [WithFile(TestImages.Jpeg.Baseline.Cmyk, nameof(CmykEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, nameof(YcckEncodingSetups), PixelTypes.Rgb24)]
    public void EncodeBaseline_NonInterleavedMode<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Retain the source profile so encoding exercises the return conversion as well as JPEG compression.
        using Image<TPixel> image = provider.GetImage();

        JpegEncoder encoder = new()
        {
            Quality = quality,
            ColorType = colorType,
            Interleaved = false,
        };
        string info = $"{colorType}-Q{quality}";

        ImageComparer comparer = new TolerantImageComparer(tolerance);

        // Does DebugSave & load reference CompareToReferenceInput():
        image.DebugSave(provider, testOutputDetails: $"{info}_Input", extension: "png");
        image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "jpg");
    }

    [Theory]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 600, 400, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 158, 24, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 153, 21, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 143, 81, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 138, 24, PixelTypes.Rgb24)]
    public void EncodeBaseline_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

    [Theory]
    [WithSolidFilledImages(nameof(NonSubsampledEncodingSetups), 1, 1, 100, 100, 100, 255, PixelTypes.L8)]
    [WithSolidFilledImages(nameof(NonSubsampledEncodingSetups), 1, 1, 255, 100, 50, 255, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 143, 81, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 7, 5, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 96, 48, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 73, 71, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 48, 24, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 46, 8, PixelTypes.Rgb24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 51, 7, PixelTypes.Rgb24)]
    public void EncodeBaseline_WithSmallImages_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, ImageComparer.Tolerant(0.12f));

    [Theory]
    [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.Rgb24, 100)]
    [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L8, 100)]
    [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.L16, 100)]
    [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La16, 100)]
    [WithSolidFilledImages(1, 1, 100, 100, 100, 255, PixelTypes.La32, 100)]
    public void EncodeBaseline_Grayscale<TPixel>(TestImageProvider<TPixel> provider, int quality)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, JpegColorType.Luminance, quality);

    [Theory]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 96, 96, PixelTypes.Rgb24 | PixelTypes.Bgr24)]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 48, 48, PixelTypes.Rgb24 | PixelTypes.Bgr24)]
    public void EncodeBaseline_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality);

    [Theory]
    [WithTestPatternImages(nameof(NonSubsampledEncodingSetups), 48, 48, PixelTypes.Rgb24 | PixelTypes.Bgr24)]
    public void EncodeBaseline_WithSmallImages_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel> => TestJpegEncoderCore(provider, colorType, quality, comparer: ImageComparer.Tolerant(0.06f));

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgb24, JpegColorType.YCbCrRatio444)]
    [WithTestPatternImages(587, 821, PixelTypes.Rgb24, JpegColorType.YCbCrRatio444)]
    [WithTestPatternImages(677, 683, PixelTypes.Rgb24, JpegColorType.YCbCrRatio420)]
    [WithSolidFilledImages(400, 400, nameof(Color.Red), PixelTypes.Rgb24, JpegColorType.YCbCrRatio420)]
    public void EncodeBaseline_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageComparer comparer = colorType == JpegColorType.YCbCrRatio444
            ? ImageComparer.TolerantPercentage(0.1f)
            : ImageComparer.TolerantPercentage(5f);

        provider.LimitAllocatorBufferCapacity().InBytesSqrt(200);
        TestJpegEncoderCore(provider, colorType, 100, comparer);
    }

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(NonSubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(SubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.BikeGrayscale, nameof(LuminanceEncodingSetups), PixelTypes.L8)]
    [WithFile(TestImages.Jpeg.Baseline.Cmyk, nameof(CmykEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, nameof(YcckEncodingSetups), PixelTypes.Rgb24)]
    public void EncodeProgressive_DefaultNumberOfScans<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Retain the source profile so encoding exercises the return conversion as well as JPEG compression.
        using Image<TPixel> image = provider.GetImage();

        JpegEncoder encoder = new()
        {
            Quality = quality,
            ColorType = colorType,
            Progressive = true
        };
        string info = $"{colorType}-Q{quality}";

        ImageComparer comparer = new TolerantImageComparer(tolerance);

        // Does DebugSave & load reference CompareToReferenceInput():
        image.DebugSave(provider, testOutputDetails: $"{info}_Input", extension: "png");
        image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "jpg");
    }

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(NonSubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(SubsampledEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Png.BikeGrayscale, nameof(LuminanceEncodingSetups), PixelTypes.L8)]
    [WithFile(TestImages.Jpeg.Baseline.Cmyk, nameof(CmykEncodingSetups), PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, nameof(YcckEncodingSetups), PixelTypes.Rgb24)]
    public void EncodeProgressive_CustomNumberOfScans<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Preserve converts the source pixels to sRGB and retains the profile for the encoder's return conversion.
        using Image<TPixel> image = provider.GetImage();

        JpegEncoder encoder = new()
        {
            Quality = quality,
            ColorType = colorType,
            Progressive = true,
            ProgressiveScans = 4,
            RestartInterval = 7
        };
        string info = $"{colorType}-Q{quality}";

        using MemoryStream ms = new();
        image.SaveAsJpeg(ms, encoder);
        ms.Position = 0;

        // TEMP: Save decoded output as PNG so we can do a pixel compare.
        using Image<TPixel> image2 = Image.Load<TPixel>(ms);
        image2.DebugSave(provider, testOutputDetails: info, extension: "png");

        ImageComparer comparer = new TolerantImageComparer(tolerance);
        image.DebugSave(provider, testOutputDetails: $"{info}_Input", extension: "png");
        image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "jpg");
    }

    [Theory]
    [InlineData(JpegColorType.YCbCrRatio420)]
    [InlineData(JpegColorType.YCbCrRatio444)]
    public async Task Encode_IsCancellable(JpegColorType colorType)
    {
        CancellationTokenSource cts = new();
        using PausedStream pausedStream = new(new MemoryStream());
        pausedStream.OnWaiting(s =>
        {
            // after some writing
            if (s.Position >= 500)
            {
                cts.Cancel();
                pausedStream.Release();
            }
            else
            {
                // allows this/next wait to unblock
                pausedStream.Next();
            }
        });

        using Image<Rgba32> image = new(5000, 5000);
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            JpegEncoder encoder = new() { ColorType = colorType };
            await image.SaveAsync(pausedStream, encoder, cts.Token);
        });
    }

    // https://github.com/SixLabors/ImageSharp/issues/2595
    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, PixelTypes.Bgra32)]
    [WithFile(TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, PixelTypes.Rgb24)]
    public static void Issue2595<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Crop(132, 1606));

        int[] quality = [100, 50];
        JpegColorType[] colors = [JpegColorType.YCbCrRatio444, JpegColorType.YCbCrRatio420];
        for (int i = 0; i < quality.Length; i++)
        {
            int q = quality[i];
            for (int j = 0; j < colors.Length; j++)
            {
                JpegColorType c = colors[j];
                image.VerifyEncoder(provider, "jpeg", $"{q}-{c}", new JpegEncoder() { Quality = q, ColorType = c }, GetComparer(q, c));
            }
        }
    }

    /// <summary>
    /// Anton's SUPER-SCIENTIFIC tolerance threshold calculation
    /// </summary>
    private static ImageComparer GetComparer(int quality, JpegColorType? colorType)
    {
        float tolerance = 0.015f; // ~1.5%

        if (quality < 50)
        {
            tolerance *= 4.5f;
        }
        else if (quality < 75 || colorType == JpegColorType.YCbCrRatio420)
        {
            tolerance *= 2.0f;
            if (colorType == JpegColorType.YCbCrRatio420)
            {
                tolerance *= 2.0f;
            }
        }

        return ImageComparer.Tolerant(tolerance);
    }

    private static void TestJpegEncoderCore<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality)
        where TPixel : unmanaged, IPixel<TPixel>
        => TestJpegEncoderCore(provider, colorType, quality, GetComparer(quality, colorType));

    private static void TestJpegEncoderCore<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, float tolerance)
        where TPixel : unmanaged, IPixel<TPixel>
        => TestJpegEncoderCore(provider, colorType, quality, new TolerantImageComparer(tolerance));

    private static void TestJpegEncoderCore<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, int quality, ImageComparer comparer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Retain the source profile so encoding exercises the return conversion as well as JPEG compression.
        using Image<TPixel> image = provider.GetImage();

        JpegEncoder encoder = new()
        {
            Quality = quality,
            ColorType = colorType
        };
        string info = $"{colorType}-Q{quality}";

        // Does DebugSave & load reference CompareToReferenceInput():
        image.DebugSave(provider, testOutputDetails: $"{info}_Input", extension: "png");
        image.VerifyEncoder(provider, "jpeg", info, encoder, comparer, referenceImageExtension: "png");
    }
}
