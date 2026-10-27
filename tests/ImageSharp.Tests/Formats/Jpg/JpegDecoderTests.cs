// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit.Abstractions;
using static SixLabors.ImageSharp.Tests.TestImages.Jpeg;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

// TODO: Scatter test cases into multiple test classes
[Trait("Format", "Jpg")]
[ValidateDisposedMemoryAllocations]
public partial class JpegDecoderTests
{
    private static MagickReferenceDecoder ReferenceDecoder => MagickReferenceDecoder.Jpeg;

    public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Argb32 | PixelTypes.Bgr24 | PixelTypes.RgbaVector;

    private const float BaselineTolerance = 0.001F / 100;

    private const float ProgressiveTolerance = 0.2F / 100;

    private static ImageComparer GetImageComparer<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string file = provider.SourceFileOrDescription;

        if (!CustomToleranceValues.TryGetValue(file, out float tolerance))
        {
            bool baseline = file.Contains("baseline", StringComparison.OrdinalIgnoreCase);
            tolerance = baseline ? BaselineTolerance : ProgressiveTolerance;
        }

        return ImageComparer.Tolerant(tolerance);
    }

    private static bool SkipTest(ITestImageProvider provider)
    {
        string[] largeImagesToSkipOn32Bit =
        [
            TestImages.Jpeg.Baseline.Jpeg420Exif,
                TestImages.Jpeg.Issues.MissingFF00ProgressiveBedroom159,
                TestImages.Jpeg.Issues.BadZigZagProgressive385,
                TestImages.Jpeg.Issues.NoEoiProgressive517,
                TestImages.Jpeg.Issues.BadRstProgressive518,
                TestImages.Jpeg.Issues.InvalidEOI695,
                TestImages.Jpeg.Issues.ExifResizeOutOfRange696,
                TestImages.Jpeg.Issues.ExifGetString750Transform
        ];

        return !TestEnvironment.Is64BitProcess && largeImagesToSkipOn32Bit.Contains(provider.SourceFileOrDescription);
    }

    public JpegDecoderTests(ITestOutputHelper output) => this.Output = output;

    private ITestOutputHelper Output { get; }

    [Fact]
    public void ParseStream_BasicPropertiesAreCorrect()
    {
        JpegDecoderOptions options = new();
        Configuration configuration = options.GeneralOptions.Configuration;
        byte[] bytes = TestFile.Create(TestImages.Jpeg.Progressive.Progress).Bytes;
        using MemoryStream ms = new(bytes);
        using JpegDecoderCore decoder = new(options);
        using Image<Rgba32> image = decoder.Decode<Rgba32>(configuration, ms, cancellationToken: default);

        // I don't know why these numbers are different. All I know is that the decoder works
        // and spectral data is exactly correct also.
        // VerifyJpeg.VerifyComponentSizes3(decoder.Frame.Components, 43, 61, 22, 31, 22, 31);
        VerifyJpeg.VerifyComponentSizes3(decoder.Frame.Components, 44, 62, 22, 31, 22, 31);
    }

    public const string DecodeBaselineJpegOutputName = "DecodeBaselineJpeg";

    [Fact]
    public void Decode_NonGeneric_CreatesRgb24Image()
    {
        string file = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Jpeg420Small);
        using Image image = Image.Load(file);
        Assert.IsType<Image<Rgb24>>(image);
    }

    [Fact]
    public async Task DecodeAsync_NonGeneric_CreatesRgb24Image()
    {
        string file = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Jpeg420Small);
        using Image image = await Image.LoadAsync(file);
        Assert.IsType<Image<Rgb24>>(image);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, CommonNonDefaultPixelTypes)]
    public void JpegDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);

        provider.Utility.TestName = DecodeBaselineJpegOutputName;
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { TargetSize = new Size { Width = 150, Height = 150 } };
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Resize_Bicubic<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            TargetSize = new Size { Width = 150, Height = 150 },
            Sampler = KnownResamplers.Bicubic
        };
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Specialized_IDCT_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new()
        {
            GeneralOptions = options,
            ResizeMode = JpegDecoderResizeMode.IdctOnly
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, specializedOptions);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Specialized_Scale_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new()
        {
            GeneralOptions = options,
            ResizeMode = JpegDecoderResizeMode.ScaleOnly
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, specializedOptions);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Specialized_Combined_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new()
        {
            GeneralOptions = options,
            ResizeMode = JpegDecoderResizeMode.Combined
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, specializedOptions);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.Tolerant(BaselineTolerance),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Progressive.Festzug, PixelTypes.Rgba32)]
    public void Decode_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InBytesSqrt(10);
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(JpegDecoder.Instance));
        this.Output.WriteLine(ex.Message);
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Floorplan, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Progressive.Festzug, PixelTypes.Rgba32)]
    public async Task DecodeAsync_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InBytesSqrt(10);
        InvalidImageContentException ex = await Assert.ThrowsAsync<InvalidImageContentException>(() => provider.GetImageAsync(JpegDecoder.Instance));
        this.Output.WriteLine(ex.Message);
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    [Theory]
    [WithFileCollection(nameof(UnsupportedTestJpegs), PixelTypes.Rgba32)]
    public void ThrowsNotSupported_WithUnsupportedJpegs<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<NotSupportedException>(() =>
        {
            using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        });

    // https://github.com/SixLabors/ImageSharp/pull/1732
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.WrongColorSpace, PixelTypes.Rgba32)]
    public void Issue1732_DecodesWithRgbColorSpace<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2057
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2057App1Parsing, PixelTypes.Rgba32)]
    public void Issue2057_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2133
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2133_DeduceColorSpace, PixelTypes.Rgba32)]
    public void Issue2133_DeduceColorSpace<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2133
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2136_ScanMarkerExtraneousBytes, PixelTypes.Rgba32)]
    public void Issue2136_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    /// <summary>
    /// Uses the JPEG defaults when an arithmetic conditioning table is not declared.
    /// </summary>
    /// <param name="originalFile">The input with explicit conditioning tables.</param>
    /// <param name="defaultFile">The input that omits default conditioning tables.</param>
    [Theory]
    [InlineData(MarkerDirectory + "arithmetic.jpg", MarkerDirectory + "arithmetic-defaults.jpg")]
    [InlineData(MarkerDirectory + "arithmetic-progressive.jpg", MarkerDirectory + "arithmetic-progressive-defaults.jpg")]
    [InlineData(MarkerDirectory + "arithmetic-progressive.jpg", MarkerDirectory + "arithmetic-progressive-default-ac.jpg")]
    public void Decode_ArithmeticConditioningDefaults_MatchExplicitTables(string originalFile, string defaultFile)
    {
        // These stored files contain identical coded scans. Libjpeg produces identical pixels with or without
        // the default DAC entries. Omission is valid JPEG syntax and does not require image-data recovery.
        using Image<Rgba32> expected = Image.Load<Rgba32>(TestFile.Create(originalFile).Bytes);
        byte[] data = TestFile.Create(defaultFile).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);

            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }
    }

    /// <summary>
    /// Checks restart recovery against clean stored files whose pixels also match in libjpeg.
    /// </summary>
    /// <param name="originalFile">The clean JPEG input.</param>
    /// <param name="malformedFile">The input with extra markers before restart markers.</param>
    [Theory]
    [InlineData(MarkerDirectory + "baseline.jpg", MarkerDirectory + "baseline-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "baseline.jpg", MarkerDirectory + "baseline-tem.jpg")]
    [InlineData(MarkerDirectory + "baseline.jpg", MarkerDirectory + "baseline-reserved-bf.jpg")]
    [InlineData(MarkerDirectory + "baseline.jpg", MarkerDirectory + "baseline-reserved-chain.jpg")]
    [InlineData(MarkerDirectory + "noninterleaved.jpg", MarkerDirectory + "noninterleaved-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "progressive.jpg", MarkerDirectory + "progressive-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "arithmetic.jpg", MarkerDirectory + "arithmetic-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "arithmetic-progressive.jpg", MarkerDirectory + "arithmetic-progressive-reserved-12.jpg")]
    public void Decode_ReservedMarkerBeforeRestartMarkers_RecoversWithIgnoreImageData(string originalFile, string malformedFile)
    {
        // These stored files have complete restart intervals. Libjpeg warns about the extra markers,
        // then finds the restart marker and produces the same pixels as the clean file.
        // The arithmetic inputs declare their conditioning tables to isolate restart recovery.
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<Rgba32> expected = Image.Load<Rgba32>(TestFile.Create(originalFile).Bytes);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, TestFile.Create(malformedFile).Bytes);

        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Rejects image-data errors when the selected policy permits only ancillary recovery or no recovery.
    /// </summary>
    /// <param name="fileName">The input with reserved markers before restart markers.</param>
    [Theory]
    [InlineData(MarkerDirectory + "baseline-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "noninterleaved-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "progressive-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "arithmetic-reserved-12.jpg")]
    [InlineData(MarkerDirectory + "arithmetic-progressive-reserved-12.jpg")]
    public void Decode_ReservedMarkerBeforeRestartMarkers_RespectsIntegrity(string fileName)
    {
        byte[] data = TestFile.Create(fileName).Bytes;
        DecoderOptions strict = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        DecoderOptions ancillary = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(strict, data);
        });

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(ancillary, data);
        });
    }

    /// <summary>
    /// Accepts a standalone TEM marker between header segments under every integrity policy.
    /// </summary>
    [Fact]
    public void Decode_TemMarkerBetweenHeaderSegments_IsSkipped()
    {
        byte[] data = TestFile.Create(MarkerDirectory + "header-tem.jpg").Bytes;
        using Image<Rgba32> expected = Image.Load<Rgba32>(TestFile.Create(MarkerDirectory + "baseline.jpg").Bytes);

        // TEM has no length field. The following bytes belong to the next marker, not to TEM.
        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            Assert.Equal(new Size(32, 24), Image.Identify(options, data).Size);

            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }
    }

    /// <summary>
    /// Keeps required header lengths valid after a standalone TEM marker.
    /// </summary>
    [Fact]
    public void Decode_InvalidQuantizationSegmentAfterTem_RemainsFatal()
    {
        // TEM precedes DQT in this stored file. DQT declares more bytes than the file contains.
        // No image data has been read, so there is no safe point for recovery.
        byte[] data = TestFile.Create(MarkerDirectory + "header-tem-invalid-dqt-length.jpg").Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
            });
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/2315
    // https://github.com/SixLabors/ImageSharp/issues/2334
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2315_NotEnoughBytes, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Issues.Issue2334_NotEnoughBytesA, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.Issues.Issue2334_NotEnoughBytesB, PixelTypes.Rgba32)]
    public void Issue2315_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2478
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2478_JFXX, PixelTypes.Rgba32)]
    public void Issue2478_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/discussions/2564
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2564, PixelTypes.Rgba32)]
    public void Issue2564_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.HangBadScan, PixelTypes.Rgb24)]
    public void DecodeHang_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<InvalidImageContentException>(
            () => { using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance); });

    // https://github.com/SixLabors/ImageSharp/issues/2517
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2517, PixelTypes.Rgba32)]
    public void Issue2517_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2638
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2638, PixelTypes.Rgba32)]
    public void Issue2638_DecodeWorks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgba32)]
    public void Decode_CMYK_ICC_Jpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.YCCK, PixelTypes.Rgba32)]
    public void Decode_YCCK_ICC_Jpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.SRgbGray, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.Perceptual, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jpeg.ICC.PerceptualcLUTOnly, PixelTypes.Rgba32)]
    public void Decode_RGB_ICC_Jpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.Issue3064, PixelTypes.Rgba32)]
    public void Decode_RGB_ICC_Jpeg_Issue3064<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    /// <summary>
    /// Verifies that converting the grayscale ICC profile preserves the sample's distinct neutral tones.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="provider">The image provider.</param>
    [Theory]
    [WithFile(TestImages.Jpeg.ICC.Issue3197, PixelTypes.Rgba32)]
    public void Decode_Grayscale_ICC_Jpeg_Issue3197<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2948
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue2948, PixelTypes.Rgb24)]
    public void Issue2948_No_SOS_Decode_Throws_InvalidImageContentException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        });

    // https://github.com/SixLabors/ImageSharp/issues/2948
    [Theory]
    [InlineData(TestImages.Jpeg.Issues.Issue2948)]
    public void Issue2948_No_SOS_Identify_Throws_InvalidImageContentException(string imagePath)
        => Assert.Throws<InvalidImageContentException>(() => _ = Image.Identify(TestFile.Create(imagePath).Bytes));

    [Fact]
    public void Issue_3071_Decode_TruncatedJpeg_Throws_InvalidImageContentException()
        => Assert.Throws<InvalidImageContentException>(() =>
        {
            // SOI marker (FF D8) + garbage bytes — only 11 bytes
            byte[] data = [0xFF, 0xD8, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30];
            using Image<Rgba32> image = Image.Load<Rgba32>(data);
        });

    // https://github.com/SixLabors/ImageSharp/issues/3118
    [Theory]
    [WithFile(TestImages.Jpeg.Issues.Issue3118, PixelTypes.Rgb24)]
    public void Issue3118_Multiple_SOF_WithSOS_DoesNotThrow<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
    }

    [Fact]
    public void Identify_MalformedApp13Segment_IgnoresNonCriticalErrorsByDefault()
    {
        ImageInfo info = Image.Identify(CreateJpegWithMalformedApp13Segment());
        Assert.True(info.Width > 0);
        Assert.True(info.Height > 0);
    }

    [Fact]
    public void Decode_MalformedApp13Segment_IgnoresNonCriticalErrorsByDefault()
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(CreateJpegWithMalformedApp13Segment());
        Assert.True(image.Width > 0);
        Assert.True(image.Height > 0);
    }

    [Fact]
    public void Identify_MalformedApp13Segment_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, CreateJpegWithMalformedApp13Segment()));
    }

    [Fact]
    public void Decode_MalformedApp13Segment_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, CreateJpegWithMalformedApp13Segment());
        });
    }

    private static byte[] CreateJpegWithMalformedApp13Segment()
    {
        byte[] source = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora).Bytes;

        // This APP13 segment starts with the valid "Photoshop 3.0\0" identifier, but the remaining
        // payload does not begin with the required "8BIM" image resource block signature.
        byte[] malformedApp13 =
        [
            0xFF, 0xED,
            0x00, 0x1D,
            (byte)'P', (byte)'h', (byte)'o', (byte)'t', (byte)'o', (byte)'s', (byte)'h', (byte)'o', (byte)'p', (byte)' ', (byte)'3', (byte)'.', (byte)'0', 0x00,
            (byte)'B', (byte)'a', (byte)'d', (byte)'R', (byte)'e', (byte)'s', (byte)'o', (byte)'u', (byte)'r', (byte)'c', (byte)'e', (byte)'!', (byte)'!'
        ];

        byte[] payload = new byte[source.Length + malformedApp13.Length];
        payload[0] = source[0];
        payload[1] = source[1];

        Buffer.BlockCopy(malformedApp13, 0, payload, 2, malformedApp13.Length);
        Buffer.BlockCopy(source, 2, payload, 2 + malformedApp13.Length, source.Length - 2);

        return payload;
    }
}
