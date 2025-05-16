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
            {
                TestImages.Jpeg.Baseline.Jpeg420Exif,
                TestImages.Jpeg.Issues.MissingFF00ProgressiveBedroom159,
                TestImages.Jpeg.Issues.BadZigZagProgressive385,
                TestImages.Jpeg.Issues.NoEoiProgressive517,
                TestImages.Jpeg.Issues.BadRstProgressive518,
                TestImages.Jpeg.Issues.InvalidEOI695,
                TestImages.Jpeg.Issues.ExifResizeOutOfRange696,
                TestImages.Jpeg.Issues.ExifGetString750Transform
            };

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
        DecoderOptions options = new() { TargetSize = new() { Width = 150, Height = 150 } };
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
            TargetSize = new() { Width = 150, Height = 150 },
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
        DecoderOptions options = new() { TargetSize = new() { Width = 150, Height = 150 } };
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
        DecoderOptions options = new() { TargetSize = new() { Width = 150, Height = 150 } };
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
        DecoderOptions options = new() { TargetSize = new() { Width = 150, Height = 150 } };
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
            GeneralOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert }
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
    public void Decode_RGB_ICC_Jpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new()
        {
            GeneralOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert }
        };

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }
}
