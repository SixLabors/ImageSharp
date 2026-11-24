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
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default));
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
        DecoderOptions options = new(Configuration.Default) { TargetSize = new Size { Width = 150, Height = 150 } };
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
    [WithFile(TestImages.Jpeg.ICC.YCCK, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Resize_FitsTargetSizeWhenIdctScaleMatchesOneDimension<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // At 1/4 IDCT scale, the decoder decodes the 2000 x 600 source to 500 x 150.
        // Only the height is equal to the target height.
        // TargetSize fits the image in the target and keeps the aspect ratio. Thus, the result is 150 x 45.
        DecoderOptions options = new(Configuration.Default) { TargetSize = new Size(150, 150) };
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);

        Assert.Equal(new Size(150, 45), image.Size);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24)]
    public void JpegDecoder_Decode_Resize_Box<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default)
        {
            // The 1/8 decode is 101 x 150 and the fitted size is 94 x 140. Thus, the decoder resizes with the sampler.
            TargetSize = new Size { Width = 140, Height = 140 },
            Sampler = KnownResamplers.Box
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
        DecoderOptions options = new(Configuration.Default) { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new(options)
        {
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
        DecoderOptions options = new(Configuration.Default) { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new(options)
        {
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
        DecoderOptions options = new(Configuration.Default) { TargetSize = new Size { Width = 150, Height = 150 } };
        JpegDecoderOptions specializedOptions = new(options)
        {
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
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
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
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
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
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        DecoderOptions ancillary = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };

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
    /// Keeps the complete MCU by default when a baseline stream ends inside the entropy-coded data of a scan.
    /// Throws when the integrity mode does not ignore image data errors.
    /// </summary>
    [Fact]
    public void Decode_TruncatedBaselineScan_RespectsIntegrity()
    {
        // The file has a restart marker after each MCU. The data of the first MCU ends at the RST0 marker at byte 667.
        // The data of the second MCU is at bytes 669 to 709, so the input ends inside it.
        byte[] complete = TestFile.Create(MarkerDirectory + "baseline.jpg").Bytes;
        byte[] data = complete.AsSpan(0, 690).ToArray();
        Assert.Equal(JpegConstants.Markers.RST0, complete[668]);

        using Image<Rgba32> expected = Image.Load<Rgba32>(complete);
        using Image<Rgba32> actual = DecodeDamagedImageData(data);
        Assert.Equal(expected.Size, actual.Size);

        // The first MCU covers 16 x 16 pixels. Chroma upsampling at the right and bottom edges of the MCU uses the
        // next MCU. Thus, these 8 x 8 pixels use only the blocks of the first MCU.
        AssertRegionsEqual(expected, actual, new Rectangle(0, 0, 8, 8));
    }

    /// <summary>
    /// Keeps the complete scans by default when a progressive stream ends inside the entropy-coded data of a scan.
    /// Throws when the integrity mode does not ignore image data errors.
    /// </summary>
    [Fact]
    public void Decode_TruncatedProgressiveScan_RespectsIntegrity()
    {
        // The fifth scan starts with its SOS marker at byte 475. It codes the luma AC coefficients of one 8 x 8 block
        // in each restart interval. Its RST markers are at bytes 486 to 516. The input ends at byte 500, so the
        // scan has data for the first six blocks and part of the seventh. The 32 x 24 image has 4 x 3 luma blocks.
        byte[] complete = TestFile.Create(MarkerDirectory + "progressive.jpg").Bytes;
        byte[] data = complete.AsSpan(0, 500).ToArray();
        Assert.Equal(JpegConstants.Markers.SOS, complete[476]);

        // The reference has the first four scans and then the EOI marker.
        byte[] reference = [.. complete.AsSpan(0, 475), 0xFF, JpegConstants.Markers.EOI];
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using Image<Rgba32> expected = Image.Load<Rgba32>(strict, reference);
        using Image<Rgba32> actual = DecodeDamagedImageData(data);
        Assert.Equal(expected.Size, actual.Size);

        // The last block row gets no data from the fifth scan, and the later scans are missing in both inputs.
        // Each luma sample uses only its own block, and the chroma data is the same in both inputs.
        AssertRegionsEqual(expected, actual, new Rectangle(0, 16, 32, 8));
    }

    /// <summary>
    /// Keeps the decoded MCU by default when a marker that is not a restart marker ends a restart interval.
    /// Throws when the integrity mode does not ignore image data errors.
    /// </summary>
    /// <param name="fileName">The complete stored input. It has a restart marker after each MCU.</param>
    /// <param name="markerCodeOffset">The offset of the code byte of the first restart marker.</param>
    [Theory]
    [InlineData(MarkerDirectory + "baseline.jpg", 668)]
    [InlineData(MarkerDirectory + "arithmetic.jpg", 243)]
    public void Decode_MissingRestartMarker_RespectsIntegrity(string fileName, int markerCodeOffset)
    {
        byte[] complete = TestFile.Create(fileName).Bytes;
        byte[] data = (byte[])complete.Clone();

        // Replace RST0 with EOI. The first MCU is complete. The second MCU has no data.
        Assert.Equal(JpegConstants.Markers.RST0, data[markerCodeOffset]);
        data[markerCodeOffset] = JpegConstants.Markers.EOI;

        using Image<Rgba32> expected = Image.Load<Rgba32>(complete);
        using Image<Rgba32> actual = DecodeDamagedImageData(data);
        Assert.Equal(expected.Size, actual.Size);

        // The first MCU covers 16 x 16 pixels. Chroma upsampling at the right and bottom edges of the MCU uses the
        // next MCU. Thus, these 8 x 8 pixels use only the blocks of the first MCU.
        AssertRegionsEqual(expected, actual, new Rectangle(0, 0, 8, 8));
    }

    /// <summary>
    /// Checks that a region of two images has the same pixels.
    /// </summary>
    /// <param name="expected">The expected image.</param>
    /// <param name="actual">The actual image.</param>
    /// <param name="region">The region to compare.</param>
    private static void AssertRegionsEqual(Image<Rgba32> expected, Image<Rgba32> actual, Rectangle region)
    {
        using Image<Rgba32> expectedRegion = expected.Clone(x => x.Crop(region));
        using Image<Rgba32> actualRegion = actual.Clone(x => x.Crop(region));
        ImageComparer.Exact.VerifySimilarity(expectedRegion, actualRegion);
    }

    /// <summary>
    /// Checks that the integrity modes that do not ignore image data errors throw for the input.
    /// Then decodes the input with the default options, which ignore image data errors.
    /// </summary>
    /// <param name="data">The input with damaged or truncated image data.</param>
    /// <returns>The image that the default options decode.</returns>
    private static Image<Rgba32> DecodeDamagedImageData(byte[] data)
    {
        foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary })
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
            });
        }

        return Image.Load<Rgba32>(data);
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
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
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
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
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
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);

        // The measured power error can change final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        // Compare each pixel in sixteen-bit units (one eight-bit code is 257), without averaging image error.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.YCCK, PixelTypes.Rgba32)]
    public void Decode_YCCK_ICC_Jpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);

        // The measured power error can change final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        // Compare each pixel in sixteen-bit units (one eight-bit code is 257), without averaging image error.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
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
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance, options);
        image.DebugSave(provider);

        // The measured power error can change final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        // Compare each pixel in sixteen-bit units (one eight-bit code is 257), without averaging image error.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);

        DecoderOptions preserveOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve, SkipMetadata = true };
        using Image<TPixel> preserved = provider.GetImage(JpegDecoder.Instance, preserveOptions);

        ImageInfo sourceInfo = Image.Identify(TestFile.Create(provider.SourceFileOrDescription).Bytes);

        ImageComparer.Exact.VerifySimilarity(image, preserved);
        Assert.Equal(sourceInfo.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());

        Assert.Null(image.Metadata.IccProfile);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.Issue3064, PixelTypes.Rgba32)]
    public void Decode_RGB_ICC_Jpeg_Issue3064<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

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
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

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
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, CreateJpegWithMalformedApp13Segment()));
    }

    [Fact]
    public void Decode_MalformedApp13Segment_ThrowsWithStrict()
    {
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
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
