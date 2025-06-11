// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats;
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

    private static string TestImageLossyHorizontalFilterPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.AlphaCompressedHorizontalFilter);

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
    [WithFile(Lossless.Animated, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossless_WithFrameDecodingModeFirst_OnlyDecodesOneFrame<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, options);
        Assert.Equal(1, image.Frames.Count);
    }

    [Theory]
    [WithFile(Lossy.AnimatedIssue2528, PixelTypes.Rgba32)]
    public void Decode_AnimatedLossy_IgnoreBackgroundColor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpDecoderOptions options = new()
        {
            BackgroundColorHandling = BackgroundColorHandling.Ignore,
            GeneralOptions = new DecoderOptions()
            {
                MaxFrames = 1
            }
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
        // Just make sure no exception is thrown. The reference decoder fails to load the image.
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Lossless.BikeThreeTransforms, PixelTypes.Rgba32)]
    public void WebpDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            TargetSize = new() { Width = 150, Height = 150 }
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
            new WebpDecoderOptions() { BackgroundColorHandling = BackgroundColorHandling.Ignore });

        // We can't use the reference decoder here.
        // It creates frames of different size without blending the frames.
        image.DebugSave(provider, extension: "webp", encoder: new WebpEncoder());
    }

    [Theory]
    [WithFile(Lossless.LossLessCorruptImage3, PixelTypes.Rgba32)]
    public void WebpDecoder_ThrowImageFormatException_OnInvalidImages<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        Assert.Throws<ImageFormatException>(
            () =>
            {
                using (provider.GetImage(WebpDecoder.Instance))
                {
                }
            });

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
    public void DecodeLossyWithHorizontalFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithHorizontalFilter, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithVerticalFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithVerticalFilter, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithSimpleFilterTest_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithSimpleFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void DecodeLossyWithComplexFilterTest_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunDecodeLossyWithComplexFilterTest, HwIntrinsics.DisableHWIntrinsic);

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
}
