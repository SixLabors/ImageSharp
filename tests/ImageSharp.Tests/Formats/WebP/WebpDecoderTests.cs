// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Webp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
[ValidateDisposedMemoryAllocations]
public class WebpDecoderTests
{
    private static MagickReferenceDecoder ReferenceDecoder => MagickReferenceDecoder.WebP;

    private static string TestImageLossyHorizontalFilterPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.AlphaCompressedHorizontalFilter);

    private static string TestImageLossyVerticalFilterPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.AlphaCompressedVerticalFilter);

    private static string TestImageLossySimpleFilterPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.SimpleFilter02);

    private static string TestImageLossyComplexFilterPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.BikeComplexFilter);

    [Theory]
    [InlineData(Lossless.GreenTransform1, 1000, 307, 32)]
    [InlineData(Lossless.BikeThreeTransforms, 250, 195, 24)]
    [InlineData(Lossless.NoTransform2, 128, 128, 24)]
    [InlineData(Lossy.Alpha1, 1000, 307, 32)]
    [InlineData(Lossy.Alpha2, 1000, 307, 32)]
    [InlineData(Lossy.BikeWithExif, 250, 195, 24)]
    public void Identify_DetectsCorrectDimensionsAndBitDepth(
        string imagePath,
        int expectedWidth,
        int expectedHeight,
        int expectedBitsPerPixel)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        Assert.NotNull(imageInfo);
        Assert.Equal(expectedWidth, imageInfo.Width);
        Assert.Equal(expectedHeight, imageInfo.Height);
        Assert.Equal(expectedBitsPerPixel, imageInfo.PixelType.BitsPerPixel);
    }

    [Theory]
    [WithFile(Lossy.BikeWithExif, PixelTypes.Rgba32)]
    [WithFile(Lossy.NoFilter01, PixelTypes.Rgba32)]
    [WithFile(Lossy.NoFilter02, PixelTypes.Rgba32)]
    [WithFile(Lossy.NoFilter03, PixelTypes.Rgba32)]
    [WithFile(Lossy.NoFilter04, PixelTypes.Rgba32)]
    [WithFile(Lossy.NoFilter05, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationNoFilter01, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationNoFilter02, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationNoFilter03, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithoutFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.SimpleFilter01, PixelTypes.Rgba32)]
    [WithFile(Lossy.SimpleFilter02, PixelTypes.Rgba32)]
    [WithFile(Lossy.SimpleFilter03, PixelTypes.Rgba32)]
    [WithFile(Lossy.SimpleFilter04, PixelTypes.Rgba32)]
    [WithFile(Lossy.SimpleFilter05, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithSimpleFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.IccpComplexFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.VeryShort, PixelTypes.Rgba32)]
    [WithFile(Lossy.BikeComplexFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter01, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter02, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter03, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter04, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter05, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter06, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter07, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter08, PixelTypes.Rgba32)]
    [WithFile(Lossy.ComplexFilter09, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithComplexFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.Small01, PixelTypes.Rgba32)]
    [WithFile(Lossy.Small02, PixelTypes.Rgba32)]
    [WithFile(Lossy.Small03, PixelTypes.Rgba32)]
    [WithFile(Lossy.Small04, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_VerySmall<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.SegmentationNoFilter04, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationNoFilter05, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationNoFilter06, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationComplexFilter01, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationComplexFilter02, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationComplexFilter03, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationComplexFilter04, PixelTypes.Rgba32)]
    [WithFile(Lossy.SegmentationComplexFilter05, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithPartitions<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.Partitions01, PixelTypes.Rgba32)]
    [WithFile(Lossy.Partitions02, PixelTypes.Rgba32)]
    [WithFile(Lossy.Partitions03, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithSegmentation<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.Sharpness01, PixelTypes.Rgba32)]
    [WithFile(Lossy.Sharpness02, PixelTypes.Rgba32)]
    [WithFile(Lossy.Sharpness03, PixelTypes.Rgba32)]
    [WithFile(Lossy.Sharpness04, PixelTypes.Rgba32)]
    [WithFile(Lossy.Sharpness05, PixelTypes.Rgba32)]
    [WithFile(Lossy.Sharpness06, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithSharpnessLevel<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.AlphaNoCompression, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaNoCompressionNoFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaCompressedNoFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaNoCompressionHorizontalFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaNoCompressionVerticalFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaNoCompressionGradientFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaCompressedHorizontalFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaCompressedVerticalFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaCompressedGradientFilter, PixelTypes.Rgba32)]
    [WithFile(Lossy.Alpha1, PixelTypes.Rgba32)]
    [WithFile(Lossy.Alpha2, PixelTypes.Rgba32)]
    [WithFile(Lossy.Alpha3, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaThinkingSmiley, PixelTypes.Rgba32)]
    [WithFile(Lossy.AlphaSticker, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    /// <summary>
    /// Compares lossy decoding with uncompressed alpha against the reference decoder across hardware configurations.
    /// </summary>
    /// <param name="provider">The source image provider.</param>
    [Theory]
    [WithFile(Lossy.AlphaNoCompression, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossy_WithUncompressedAlpha(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunUncompressedAlphaDecode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Checks the supplied image in the selected hardware configuration.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    /// <param name="intrinsics">The hardware configuration used for output naming.</param>
    private static void RunUncompressedAlphaDecode(string providerDump, string intrinsics)
    {
        TestImageProvider<Rgba32> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);
        using Image<Rgba32> image = provider.GetImage(WebpDecoder.Instance);
        using Image<Rgba32> reference = provider.GetImage(ReferenceDecoder);

        image.DebugSave(provider, intrinsics);

        // Bound the independent decoder's byte rounding per pixel; opaque regions must not hide alpha errors.
        ImageComparer.Tolerant(0F, 3 * (ushort.MaxValue / byte.MaxValue)).VerifySimilarity(reference, image);
    }

    [Theory]
    [WithFile(Lossless.Alpha, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.NoTransform1, PixelTypes.Rgba32)]
    [WithFile(Lossless.NoTransform2, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithoutTransforms<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.GreenTransform1, PixelTypes.Rgba32)]
    [WithFile(Lossless.GreenTransform2, PixelTypes.Rgba32)]
    [WithFile(Lossless.GreenTransform3, PixelTypes.Rgba32)]
    [WithFile(Lossless.GreenTransform4, PixelTypes.Rgba32)]

    // TODO: Reference decoder throws here MagickCorruptImageErrorException, webpinfo also indicates an error here, but decoding the image seems to work.
    // [WithFile(Lossless.GreenTransform5, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithSubtractGreenTransform<TPixel>(
        TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.ColorIndexTransform1, PixelTypes.Rgba32)]
    [WithFile(Lossless.ColorIndexTransform2, PixelTypes.Rgba32)]
    [WithFile(Lossless.ColorIndexTransform3, PixelTypes.Rgba32)]
    [WithFile(Lossless.ColorIndexTransform4, PixelTypes.Rgba32)]
    [WithFile(Lossless.ColorIndexTransform5, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithColorIndexTransform<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.PredictorTransform1, PixelTypes.Rgba32)]
    [WithFile(Lossless.PredictorTransform2, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithPredictorTransform<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.CrossColorTransform1, PixelTypes.Rgba32)]
    [WithFile(Lossless.CrossColorTransform2, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithCrossColorTransform<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.TwoTransforms1, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms2, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms3, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms4, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms5, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms6, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms7, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms8, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms9, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms10, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms11, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms12, PixelTypes.Rgba32)]
    [WithFile(Lossless.TwoTransforms13, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithTwoTransforms<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.ThreeTransforms1, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms2, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms3, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms4, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms5, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms6, PixelTypes.Rgba32)]
    [WithFile(Lossless.ThreeTransforms7, PixelTypes.Rgba32)]
    [WithFile(Lossless.BikeThreeTransforms, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithThreeTransforms<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossless.Animated, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossless_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        WebpMetadata webpMetaData = image.Metadata.GetWebpMetadata();
        WebpFrameMetadata frameMetaData = image.Frames.RootFrame.Metadata.GetWebpMetadata();

        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);

        Assert.Equal(0, webpMetaData.RepeatCount);
        Assert.Equal(150U, frameMetaData.FrameDelay);
        Assert.Equal(12, image.Frames.Count);
    }

    [Theory]
    [InlineData(Lossless.Animated)]
    public void Info_AnimatedLossless_VerifyAllFrames(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = WebpDecoder.Instance.Identify(DecoderOptions.Default, stream);
        WebpMetadata webpMetaData = image.Metadata.GetWebpMetadata();
        WebpFrameMetadata frameMetaData = image.FrameMetadataCollection[0].GetWebpMetadata();

        Assert.Equal(0, webpMetaData.RepeatCount);
        Assert.Equal(150U, frameMetaData.FrameDelay);
        Assert.Equal(12, image.FrameCount);
    }

    [Theory]
    [WithFile(Lossy.Animated, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossy_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        WebpMetadata webpMetaData = image.Metadata.GetWebpMetadata();
        WebpFrameMetadata frameMetaData = image.Frames.RootFrame.Metadata.GetWebpMetadata();

        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Tolerant(0.04f));

        Assert.Equal(0, webpMetaData.RepeatCount);
        Assert.Equal(150U, frameMetaData.FrameDelay);
        Assert.Equal(12, image.Frames.Count);
    }

    [Theory]
    [InlineData(Lossy.Animated)]
    public void Info_AnimatedLossy_VerifyAllFrames(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = WebpDecoder.Instance.Identify(DecoderOptions.Default, stream);
        WebpMetadata webpMetaData = image.Metadata.GetWebpMetadata();
        WebpFrameMetadata frameMetaData = image.FrameMetadataCollection[0].GetWebpMetadata();

        Assert.Equal(0, webpMetaData.RepeatCount);
        Assert.Equal(150U, frameMetaData.FrameDelay);
        Assert.Equal(12, image.FrameCount);
    }

    [Theory]
    [WithFile(Lossless.Animated, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossless_WithFrameDecodingModeFirst_OnlyDecodesOneFrame<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default) { MaxFrames = 1 };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        Assert.Equal(1, image.Frames.Count);
    }

    [Theory]
    [WithFile(Lossy.AnimatedIssue2528, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossy_IgnoreBackgroundColor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpDecoderOptions options = new(new DecoderOptions(Configuration.Default)
            {
                MaxFrames = 1
            })
{
            BackgroundColorHandling = BackgroundColorHandling.Ignore
};
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.AnimatedLandscape, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossy_AlphaBlending_Works<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSaveMultiFrame(provider);
        image.CompareToOriginalMultiFrame(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(Lossless.LossLessCorruptImage1, PixelTypes.Rgba32)]
    [WithFile(Lossless.LossLessCorruptImage2, PixelTypes.Rgba32)]
    [WithFile(Lossless.LossLessCorruptImage4, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Lossless_WithIssues<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // These stored files end before their declared VP8L payload ends. Local libwebp reports NOT_ENOUGH_DATA.
        // Recovery belongs to IgnoreImageData; ignoring ancillary errors must not permit missing image bytes.
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        DecoderOptions ancillary = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };
        Assert.Throws<InvalidImageContentException>(() => provider.GetImage(WebpDecoder.Instance, strict));
        Assert.Throws<InvalidImageContentException>(() => provider.GetImage(WebpDecoder.Instance, ancillary));

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Lossless.BikeThreeTransforms, PixelTypes.Rgba32)]
    public void WebpDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default)
        {
            TargetSize = new Size { Width = 150, Height = 150 }
        };

        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

        // Floating point differences in FMA used in the ResizeKernel result in minor pixel differences.
        // Output have been manually verified.
        // For more details see discussion: https://github.com/SixLabors/ImageSharp/pull/1513#issuecomment-763643594
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(Fma.IsSupported ? 0.0007F : 0.0156F),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1594
    [Theory]
    [WithFile(Lossy.Issue1594, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue1594<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2243
    [Theory]
    [WithFile(Lossy.Issue2243, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2243<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2257
    [Theory]
    [WithFile(Lossy.Issue2257, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2257<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2670
    [Theory]
    [WithFile(Lossy.Issue2670, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2670<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2866
    [Theory]
    [WithFile(Lossy.Issue2866, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2866<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Web
        using Image<TPixel> image = provider.GetImage(
            WebpDecoder.Instance,
            new WebpDecoderOptions(new DecoderOptions(provider.Configuration)) { BackgroundColorHandling = BackgroundColorHandling.Ignore });

        // We can't use the reference decoder here.
        // It creates frames of different size without blending the frames.
        image.DebugSave(provider, extension: "webp", encoder: new WebpEncoder());
    }

    /// <summary>
    /// A file with a backward reference that is not valid and a missing tail decodes with the default options.
    /// The reference is in the main pixel data, at pixel 48789 of the 512 x 512 image. The transforms are valid.
    /// Strict mode throws for the damaged image data.
    /// </summary>
    [Theory]
    [WithFile(Lossless.LossLessCorruptImage3, PixelTypes.Rgba32)]
    public void WebpDecoder_DamagedLosslessImageData_DecodesByDefaultAndThrowsWithStrict<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => provider.GetImage(WebpDecoder.Instance, strict));

        // The default mode is IgnoreImageData. It returns the pixels that it can decode and does not throw.
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, new DecoderOptions(Configuration.Default));
        image.DebugSave(provider);
    }

    /// <summary>
    /// A still image whose image data ends early decodes with the default options. The rows before the end
    /// match a full decode, and the rows after it stay cleared. Strict mode throws.
    /// </summary>
    /// <param name="fileFormat">The WebP bitstream to test.</param>
    /// <param name="endsAtEof">
    /// True when the file ends inside the image chunk. False when the image chunk is complete but its
    /// bitstream ends early.
    /// </param>
    [Theory]
    [InlineData(WebpFileFormatType.Lossy, true)]
    [InlineData(WebpFileFormatType.Lossy, false)]
    [InlineData(WebpFileFormatType.Lossless, true)]
    [InlineData(WebpFileFormatType.Lossless, false)]
    public void Decode_TruncatedImageData_KeepsDecodedRowsByDefaultAndThrowsWithStrict(WebpFileFormatType fileFormat, bool endsAtEof)
    {
        using Image<Rgb24> source = CreateNoiseImage(64, 64, 1);
        using MemoryStream stream = new();
        source.Save(stream, new WebpEncoder { FileFormat = fileFormat, Quality = 90 });
        byte[] data = stream.ToArray();

        string fourCc = fileFormat == WebpFileFormatType.Lossy ? "VP8 " : "VP8L";
        int chunk = FindChunks(data, fourCc)[0];
        int payloadSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(chunk + 4));

        // Keep the fixed VP8 header and the first partition, so that only the token data ends early.
        // The 19-bit first partition size is in the 3-byte frame tag. See RFC 6386, section 9.1.
        int keep = payloadSize / 2;
        if (fileFormat == WebpFileFormatType.Lossy)
        {
            int frameTag = data[chunk + 8] | (data[chunk + 9] << 8) | (data[chunk + 10] << 16);
            int firstPartitionSize = frameTag >> 5;
            keep = 10 + firstPartitionSize + ((payloadSize - 10 - firstPartitionSize) / 2);
        }

        byte[] truncated = Truncate(data, chunk, keep & ~1, !endsAtEof);

        // The decoder writes whole rows in row order. The rows after the end keep the cleared value.
        using Image<Rgba32> full = Image.Load<Rgba32>(data);
        using Image<Rgba32> actual = Image.Load<Rgba32>(truncated);
        using Image<Rgba32> expected = PartialDecodeExpectation.Create(full.Frames.RootFrame, actual.Frames.RootFrame, actual.Width, false, _ => default);
        ImageComparer.Exact.VerifySimilarity(expected, actual);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        DecoderOptions ancillary = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreAncillary };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, truncated));
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(ancillary, truncated));
    }

    /// <summary>
    /// An animated image whose last frame ends at EOF decodes with the default options. The first frame matches
    /// a full decode, and the last frame keeps the rows that it can decode. Strict mode throws.
    /// </summary>
    [Fact]
    public void Decode_TruncatedAnimationFrame_KeepsDecodedFramesByDefaultAndThrowsWithStrict()
    {
        using Image<Rgb24> source = CreateNoiseImage(64, 64, 1);
        using (Image<Rgb24> second = CreateNoiseImage(64, 64, 2))
        {
            // Source blending copies the decoded frame to the canvas, so the rows that are not decoded stay cleared.
            source.Frames.AddFrame(second.Frames.RootFrame).Metadata.GetWebpMetadata().BlendMode = FrameBlendMode.Source;
        }

        source.Frames.RootFrame.Metadata.GetWebpMetadata().BlendMode = FrameBlendMode.Source;

        using MemoryStream stream = new();
        source.Save(stream, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
        byte[] data = stream.ToArray();

        // An ANMF payload starts with a 16-byte frame header. The frame image chunk follows it.
        List<int> frames = FindChunks(data, "ANMF");
        Assert.Equal(2, frames.Count);
        int imageChunk = frames[1] + 8 + 16;
        int imageSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(imageChunk + 4));
        byte[] truncated = data.AsSpan(0, imageChunk + 8 + (imageSize / 2)).ToArray();

        // The first frame is complete. The last frame keeps its whole decoded rows, and its other rows keep the cleared value.
        using Image<Rgba32> full = Image.Load<Rgba32>(data);
        using Image<Rgba32> actual = Image.Load<Rgba32>(truncated);
        using Image<Rgba32> expected = full.Frames.CloneFrame(0);
        using (Image<Rgba32> lastFrame = PartialDecodeExpectation.Create(full.Frames[1], actual.Frames[1], actual.Width, false, _ => default))
        {
            expected.Frames.AddFrame(lastFrame.Frames.RootFrame);
        }

        ImageComparer.Exact.VerifySimilarity(expected, actual);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, truncated));
    }

    /// <summary>
    /// A lossy image whose ALPH data ends early decodes with the default options. The color rows decode, the alpha
    /// rows before the end match a full decode, and the alpha values after it are 0. Strict mode throws.
    /// </summary>
    [Fact]
    public void Decode_TruncatedAlphaData_KeepsDecodedAlphaByDefaultAndThrowsWithStrict()
    {
        Random random = new(3);
        using Image<Rgba32> source = new(64, 64);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                source[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }

        using MemoryStream stream = new();
        source.Save(stream, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy });
        byte[] data = stream.ToArray();

        // Halve the ALPH payload and set the ALPH and RIFF sizes to the new length. The VP8 chunk follows unchanged,
        // so only the alpha bitstream ends early.
        int alpha = FindChunks(data, "ALPH")[0];
        int alphaSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(alpha + 4));
        int alphaEnd = alpha + 8 + alphaSize + (alphaSize & 1);
        int keep = (alphaSize / 2) & ~1;
        byte[] truncated = [.. data.AsSpan(0, alpha + 8 + keep), .. data.AsSpan(alphaEnd)];
        BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(alpha + 4), (uint)keep);
        BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(4), (uint)(truncated.Length - 8));

        // The color data is complete. The alpha decoder writes whole rows in row order, and the alpha values of the
        // rows after the end are 0.
        using Image<Rgba32> full = Image.Load<Rgba32>(data);
        using Image<Rgba32> actual = Image.Load<Rgba32>(truncated);
        using Image<Rgba32> expected = PartialDecodeExpectation.Create(
            full.Frames.RootFrame,
            actual.Frames.RootFrame,
            actual.Width,
            false,
            pixel => new Rgba32(pixel.R, pixel.G, pixel.B, 0));

        ImageComparer.Exact.VerifySimilarity(expected, actual);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, truncated));
    }

    /// <summary>
    /// Creates an image of fixed pseudo-random noise. Noise is hard to compress, so most of the encoded data is pixel data.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="seed">The seed of the noise.</param>
    /// <returns>The image.</returns>
    private static Image<Rgb24> CreateNoiseImage(int width, int height, int seed)
    {
        Random random = new(seed);
        Image<Rgb24> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }

        return image;
    }

    /// <summary>
    /// Gets the offsets of the top-level RIFF chunks with the given FourCC.
    /// </summary>
    /// <param name="data">The WebP file.</param>
    /// <param name="fourCc">The chunk FourCC.</param>
    /// <returns>The offsets of the chunk headers.</returns>
    private static List<int> FindChunks(byte[] data, string fourCc)
    {
        List<int> offsets = [];

        // The first chunk follows the 12-byte RIFF header. Each chunk has an 8-byte header and an even padded size.
        int offset = 12;
        while (offset + 8 <= data.Length)
        {
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
            if (System.Text.Encoding.ASCII.GetString(data, offset, 4) == fourCc)
            {
                offsets.Add(offset);
            }

            offset += 8 + size + (size & 1);
        }

        return offsets;
    }

    /// <summary>
    /// Cuts a chunk payload. The file can end inside the chunk. Or, the chunk and the RIFF sizes can be set to the new
    /// length, so that only the bitstream ends early.
    /// </summary>
    /// <param name="data">The WebP file.</param>
    /// <param name="chunk">The offset of the chunk header.</param>
    /// <param name="payloadSize">The payload bytes to keep. It is even, so the chunk needs no padding byte.</param>
    /// <param name="updateSizes">True to set the chunk and the RIFF sizes to the new length.</param>
    /// <returns>The changed file.</returns>
    private static byte[] Truncate(byte[] data, int chunk, int payloadSize, bool updateSizes)
    {
        byte[] result = data.AsSpan(0, chunk + 8 + payloadSize).ToArray();
        if (updateSizes)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(chunk + 4), (uint)payloadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        }

        return result;
    }

    private static void RunDecodeLossyWithHorizontalFilter()
    {
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImageLossyHorizontalFilterPath);
        using Image<Rgba32> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    private static void RunDecodeLossyWithVerticalFilter()
    {
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImageLossyVerticalFilterPath);
        using Image<Rgba32> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    private static void RunDecodeLossyWithSimpleFilterTest()
    {
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImageLossySimpleFilterPath);
        using Image<Rgba32> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    private static void RunDecodeLossyWithComplexFilterTest()
    {
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImageLossyComplexFilterPath);
        using Image<Rgba32> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Fact]
    public void DecodeLossyWithHorizontalFilter_WithoutHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithHorizontalFilter, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithVerticalFilter_WithoutHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithVerticalFilter, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithSimpleFilterTest_WithoutHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithSimpleFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithComplexFilterTest_WithoutHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithComplexFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Theory]
    [InlineData(Lossy.BikeWithExif)]
    public void Decode_VerifyRatio(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        using Image image = WebpDecoder.Instance.Decode(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;

        Assert.Equal(37.8, meta.HorizontalResolution);
        Assert.Equal(37.8, meta.VerticalResolution);
        Assert.Equal(PixelResolutionUnit.PixelsPerCentimeter, meta.ResolutionUnits);
    }

    [Theory]
    [InlineData(Lossy.BikeWithExif)]
    public void Identify_VerifyRatio(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo image = WebpDecoder.Instance.Identify(DecoderOptions.Default, stream);
        ImageMetadata meta = image.Metadata;

        Assert.Equal(37.8, meta.HorizontalResolution);
        Assert.Equal(37.8, meta.VerticalResolution);
        Assert.Equal(PixelResolutionUnit.PixelsPerCentimeter, meta.ResolutionUnits);
    }

    [Theory]
    [WithFile(Lossy.Issue2925, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2925<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Lossy.Issue2906, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2906<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);

        ExifProfile exifProfile = image.Metadata.ExifProfile;
        IExifValue<EncodedString> comment = exifProfile.GetValue(ExifTag.UserComment);

        Assert.NotNull(comment);
        Assert.Equal(EncodedString.CharacterCode.Unicode, comment.Value.Code);
        Assert.StartsWith("1girl, pariya, ", comment.Value.Text);

        image.DebugSave(provider);
        image.CompareToOriginal(provider, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Icc.Perceptual, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualcLUTOnly, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(
            WebpDecoder.Instance,
            new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert });

        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
        Assert.Null(image.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks ICC color conversion and alpha storage with straight and associated destination pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="provider">The source image provider.</param>
    [Theory]
    [WithFile(Lossy.WithIccpAndAlpha, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(Lossless.WithIccpAndAlpha, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    public void Decode_IccWithAlpha_UsesDestinationAlphaRepresentation<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            PixelTests<TPixel>.RunIccAlphaDecode,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512v2 | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares decoded pixels with an independent ICC transform in the requested pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="provider">The source image provider.</param>
    /// <param name="intrinsics">The hardware configuration used for output naming.</param>
    private static void AssertIccAlphaDecode<TPixel>(TestImageProvider<TPixel> provider, string intrinsics)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> actual = provider.GetImage(WebpDecoder.Instance);

        // These pixels are now sRGB. Do not attach the retained source profile to the saved comparison image.
        actual.Metadata.IccProfile = null;
        PngEncoder encoder = new() { BitDepth = PngBitDepth.Bit16 };
        actual.DebugSave(provider, intrinsics, extension: "png", encoder: encoder);

        // These eight-bit fixtures include partial and zero alpha. Allow three codes in total (771 sixteen-bit units) per pixel.
        // Any pixel whose total component difference exceeds this limit fails the comparison.
        actual.CompareToOriginal(provider, ImageComparer.Tolerant(0F, 3 * (ushort.MaxValue / byte.MaxValue)), ReferenceDecoder);
    }

    /// <summary>
    /// Provides non-generic callbacks whose declaring type retains the requested pixel format for RemoteExecutor.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    private static class PixelTests<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Restores the typed provider and runs the assertions with the selected hardware features.
        /// </summary>
        /// <param name="providerDump">The serialized image provider.</param>
        /// <param name="intrinsics">The hardware configuration used for output naming.</param>
        public static void RunIccAlphaDecode(string providerDump, string intrinsics)
            => AssertIccAlphaDecode(FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump), intrinsics);
    }

    /// <summary>
    /// Checks that ignored container fields and data do not change pixels or metadata.
    /// </summary>
    [Theory]
    [InlineData(ContainerDirectory + "odd-metadata.webp", ContainerDirectory + "reserved-fields.webp")]
    [InlineData(Lossy.Small02, ContainerDirectory + "lossy-trailing.webp")]
    [InlineData(Lossless.NoTransform1, ContainerDirectory + "lossless-trailing.webp")]
    [InlineData(Lossy.AlphaCompressedHorizontalFilter, ContainerDirectory + "alpha-trailing.webp")]
    [InlineData(ContainerDirectory + "odd-metadata.webp", ContainerDirectory + "metadata-trailing.webp")]
    [InlineData(ContainerDirectory + "odd-metadata.webp", ContainerDirectory + "unknown-signed.webp")]
    [InlineData(ContainerDirectory + "odd-metadata.webp", ContainerDirectory + "unknown-unsigned.webp")]
    [InlineData(ContainerDirectory + "odd-metadata.webp", ContainerDirectory + "unknown-maximum.webp")]
    [InlineData(ContainerDirectory + "animation.webp", ContainerDirectory + "animation-trailing.webp")]
    [InlineData(ContainerDirectory + "animation.webp", ContainerDirectory + "animation-unknown.webp")]
    public void Decode_ContainerVariations_PreservePixelsAndMetadata(string originalFile, string modifiedFile)
    {
        // Local libwebp confirms identical still pixels and two frames for each animation variant.
        // The stored files alter reserved fields, append bytes outside RIFF, or add an unknown chunk.
        byte[] original = TestFile.Create(originalFile).Bytes;
        byte[] modified = TestFile.Create(modifiedFile).Bytes;
        DecoderOptions options = new(Configuration.Default) { MaxFrames = 2 };
        using Image<Rgba32> expected = Image.Load<Rgba32>(options, original);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, modified);

        ImageComparer.Exact.VerifySimilarity(expected, actual);
        Assert.Equal(expected.Frames.Count, actual.Frames.Count);
        Assert.Equal(expected.Metadata.ExifProfile?.ToByteArray(), actual.Metadata.ExifProfile?.ToByteArray());
        Assert.Equal(expected.Metadata.XmpProfile?.ToByteArray(), actual.Metadata.XmpProfile?.ToByteArray());
        Assert.Equal(expected.Metadata.IccProfile?.ToByteArray(), actual.Metadata.IccProfile?.ToByteArray());

        ImageInfo expectedInfo = Image.Identify(options, original);
        ImageInfo actualInfo = Image.Identify(options, modified);
        Assert.Equal(expectedInfo.Size, actualInfo.Size);
        Assert.Equal(expectedInfo.FrameMetadataCollection.Count, actualInfo.FrameMetadataCollection.Count);
    }

    /// <summary>
    /// Rejects headers and image payloads whose declared extent exceeds RIFF.
    /// </summary>
    [Theory]
    [InlineData(ContainerDirectory + "vp8-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "vp8l-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "vp8x-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "alpha-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "anim-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "frame-crosses-riff.webp")]
    [InlineData(ContainerDirectory + "vp8l-short-header.webp")]
    [InlineData(ContainerDirectory + "vp8x-short-header.webp")]
    public void Decode_ChunkOutsideRiff_RejectsInvalidFraming(string fileName)
    {
        byte[] data = TestFile.Create(fileName).Bytes;

        // These files retain bytes after RIFF. Those bytes must not complete a header or image payload.
        // Framing errors cannot be recovered by ignoring image or ancillary segment contents.
        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() => Image.Load(options, data));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
        }
    }

    /// <summary>
    /// Rejects an image payload that borrows bytes from the next animation frame.
    /// </summary>
    [Fact]
    public void Decode_FrameImageOutsideAnmf_RejectsInvalidFraming()
    {
        // The VP8L payload extends into the next ANMF chunk. Local libwebp rejects this file.
        byte[] data = TestFile.Create(ContainerDirectory + "frame-crosses-anmf.webp").Bytes;
        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() => Image.Load(options, data));
        }
    }
}
