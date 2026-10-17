// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;
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
    /// <summary>
    /// The kinds of encoded files the container tests mutate.
    /// </summary>
    public enum ContainerSample
    {
        /// <summary>
        /// A simple lossy file with only a VP8 chunk.
        /// </summary>
        Lossy,

        /// <summary>
        /// A simple lossless file with only a VP8L chunk.
        /// </summary>
        Lossless,

        /// <summary>
        /// An extended lossy file with VP8X, ALPH and VP8 chunks.
        /// </summary>
        LossyWithAlpha,

        /// <summary>
        /// An extended lossless file with VP8X, VP8L and EXIF chunks.
        /// </summary>
        LosslessWithExif,

        /// <summary>
        /// An extended lossy file with VP8X, VP8, EXIF and XMP chunks.
        /// </summary>
        LossyWithExifAndXmp,

        /// <summary>
        /// An extended lossless file with VP8X, ICCP and VP8L chunks.
        /// </summary>
        LosslessWithIcc
    }

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
            GeneralOptions = new DecoderOptions
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
            new WebpDecoderOptions { BackgroundColorHandling = BackgroundColorHandling.Ignore });

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

    [Theory]
    [WithFile(Icc.Perceptual, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualcLUTOnly, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance, new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert });

        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
        Assert.Null(image.Metadata.IccProfile);
    }

    [Theory]
    [InlineData(20, 0x40)]
    [InlineData(20, 0x80)]
    [InlineData(20, 0x01)]
    [InlineData(21, 0xFF)]
    [InlineData(23, 0x80)]
    public void Decode_Vp8XWithNonZeroReservedFields_MatchesOriginal(int offset, byte bits)
    {
        // Offset 20 is the VP8X feature flags byte, whose two top bits and lowest bit are reserved.
        // Offsets 21 to 23 are the reserved bytes that follow it.
        byte[] original = EncodeContainerSample(ContainerSample.LosslessWithExif);
        byte[] patched = (byte[])original.Clone();
        patched[offset] |= bits;

        AssertSameDecodedImage(original, patched);
    }

    [Theory]
    [InlineData(ContainerSample.Lossy)]
    [InlineData(ContainerSample.Lossless)]
    [InlineData(ContainerSample.LossyWithAlpha)]
    [InlineData(ContainerSample.LosslessWithExif)]
    [InlineData(ContainerSample.LossyWithExifAndXmp)]
    [InlineData(ContainerSample.LosslessWithIcc)]
    public void Decode_WithBytesAfterRiffPayload_MatchesOriginal(ContainerSample sample)
    {
        byte[] original = EncodeContainerSample(sample);
        Random random = new(42);
        for (int count = 1; count <= 64; count++)
        {
            byte[] patched = new byte[original.Length + count];
            original.CopyTo(patched, 0);
            random.NextBytes(patched.AsSpan(original.Length));

            AssertSameDecodedImage(original, patched);
        }
    }

    [Fact]
    public void Decode_WithImageChunkAfterRiffPayload_IgnoresIt()
    {
        // A complete chunk after the RIFF payload is not part of the file, even when it is valid image data.
        byte[] original = EncodeContainerSample(ContainerSample.LossyWithAlpha);
        using Image<Rgba32> other = new(5, 3, Color.White.ToPixel<Rgba32>());
        using MemoryStream otherStream = new();
        other.Save(otherStream, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
        byte[] otherData = otherStream.ToArray();
        Assert.Equal(12, FindChunk(otherData, "VP8L"));

        byte[] patched = [.. original, .. otherData.AsSpan(12)];

        AssertSameDecodedImage(original, patched);
    }

    [Theory]
    [InlineData(0x7FFFFFF0U)]
    [InlineData(0xFFFFFFF0U)]
    public void Decode_UnknownChunkLargerThanRiffPayload_MatchesOriginal(uint size)
    {
        // The declared size runs past the end of the RIFF payload, and the second one is negative as an int.
        byte[] original = EncodeContainerSample(ContainerSample.LosslessWithExif);
        byte[] patched = [.. original, .. "ABCD"u8, 0, 0, 0, 0, 1, 2, 3];
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(original.Length + 4), size);
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(4), (uint)patched.Length - 8);

        AssertSameDecodedImage(original, patched);
    }

    [Theory]
    [InlineData(ContainerSample.Lossy)]
    [InlineData(ContainerSample.Lossless)]
    [InlineData(ContainerSample.LossyWithAlpha)]
    [InlineData(ContainerSample.LosslessWithExif)]
    [InlineData(ContainerSample.LossyWithExifAndXmp)]
    [InlineData(ContainerSample.LosslessWithIcc)]
    public void Decode_TruncatedContainer_DecodesOrThrowsInvalidImageContentException(ContainerSample sample)
    {
        byte[] original = EncodeContainerSample(sample);

        // Data missing from the compressed image data is reported by the bitstream decoders rather than the
        // container parser, so Load is only checked for cuts outside that data. Identify only reads its header.
        bool isLossless = TryFindChunk(original, "VP8L", out int imageChunk);
        if (!isLossless)
        {
            imageChunk = FindChunk(original, "VP8 ");
        }

        int bitstreamStart = imageChunk + WebpConstants.ChunkHeaderSize + (isLossless ? 5 : WebpConstants.Vp8FrameHeaderSize);
        int bitstreamEnd = imageChunk + WebpConstants.ChunkHeaderSize + BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(imageChunk + 4));

        // The format detector needs the complete 12 byte RIFF header to recognize the file.
        for (int length = WebpConstants.RiffHeaderSize; length < original.Length; length++)
        {
            byte[] truncated = original.AsSpan(0, length).ToArray();
            AssertDecodesOrThrowsInvalidImageContent(() => _ = Image.Identify(truncated), length);
            if (length < bitstreamStart || length >= bitstreamEnd)
            {
                AssertDecodesOrThrowsInvalidImageContent(
                    () =>
                    {
                        using Image<Rgba32> image = Image.Load<Rgba32>(truncated);
                    },
                    length);
            }
        }
    }

    /// <summary>
    /// Encodes a small image whose container has the chunks described by <paramref name="sample"/>.
    /// </summary>
    /// <param name="sample">The kind of file to encode.</param>
    /// <returns>The encoded file.</returns>
    private static byte[] EncodeContainerSample(ContainerSample sample)
    {
        bool lossless = sample is ContainerSample.Lossless or ContainerSample.LosslessWithExif or ContainerSample.LosslessWithIcc;
        bool alpha = sample is ContainerSample.LossyWithAlpha or ContainerSample.LosslessWithExif;

        using Image<Rgba32> image = new(23, 17);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    byte a = alpha ? (byte)(255 - (x * 9)) : (byte)255;
                    row[x] = new Rgba32((byte)(x * 11), (byte)(y * 15), (byte)((x + y) * 6), a);
                }
            }
        });

        if (sample is ContainerSample.LosslessWithExif or ContainerSample.LossyWithExifAndXmp)
        {
            ExifProfile exifProfile = new();
            exifProfile.SetValue(ExifTag.Software, "ImageSharp");
            image.Metadata.ExifProfile = exifProfile;
        }

        if (sample is ContainerSample.LossyWithExifAndXmp)
        {
            image.Metadata.XmpProfile = new XmpProfile("<x:xmpmeta xmlns:x='adobe:ns:meta/'></x:xmpmeta>"u8.ToArray());
        }

        if (sample is ContainerSample.LosslessWithIcc)
        {
            image.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.ProfileRandomArray);
        }

        using MemoryStream stream = new();
        image.Save(stream, new WebpEncoder { FileFormat = lossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy });
        return stream.ToArray();
    }

    /// <summary>
    /// Asserts that both files decode and identify to the same pixels, metadata and image information.
    /// </summary>
    /// <param name="expectedData">The unmodified file.</param>
    /// <param name="actualData">The modified file.</param>
    private static void AssertSameDecodedImage(byte[] expectedData, byte[] actualData)
    {
        using Image<Rgba32> expected = Image.Load<Rgba32>(expectedData);
        using Image<Rgba32> actual = Image.Load<Rgba32>(actualData);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
        Assert.Equal(expected.Metadata.ExifProfile?.ToByteArray(), actual.Metadata.ExifProfile?.ToByteArray());
        Assert.Equal(expected.Metadata.XmpProfile?.ToByteArray(), actual.Metadata.XmpProfile?.ToByteArray());
        Assert.Equal(expected.Metadata.IccProfile?.ToByteArray(), actual.Metadata.IccProfile?.ToByteArray());

        ImageInfo expectedInfo = Image.Identify(expectedData);
        ImageInfo actualInfo = Image.Identify(actualData);
        Assert.Equal(expectedInfo.Size, actualInfo.Size);
        Assert.Equal(expectedInfo.PixelType.BitsPerPixel, actualInfo.PixelType.BitsPerPixel);
    }

    /// <summary>
    /// Asserts that an operation on a truncated file either succeeds or reports invalid image content.
    /// </summary>
    /// <param name="operation">The decoding operation.</param>
    /// <param name="length">The truncated file length, reported on failure.</param>
    private static void AssertDecodesOrThrowsInvalidImageContent(Action operation, int length)
    {
        Exception exception = Record.Exception(operation);
        Assert.True(exception is null or InvalidImageContentException, $"Truncated to {length} bytes: {exception}");
    }

    /// <summary>
    /// Gets the offset of the first chunk with the given FourCC.
    /// </summary>
    /// <param name="data">The encoded file.</param>
    /// <param name="fourCc">The chunk FourCC.</param>
    /// <returns>The offset of the chunk header.</returns>
    private static int FindChunk(byte[] data, string fourCc)
    {
        Assert.True(TryFindChunk(data, fourCc, out int offset), $"Chunk '{fourCc}' not found.");
        return offset;
    }

    /// <summary>
    /// Walks the chunks after the RIFF header to find the first chunk with the given FourCC.
    /// </summary>
    /// <param name="data">The encoded file.</param>
    /// <param name="fourCc">The chunk FourCC.</param>
    /// <param name="offset">The offset of the chunk header, when found.</param>
    /// <returns>Whether the chunk was found.</returns>
    private static bool TryFindChunk(byte[] data, string fourCc, out int offset)
    {
        offset = WebpConstants.RiffHeaderSize;
        while (offset + WebpConstants.ChunkHeaderSize <= data.Length)
        {
            if (Encoding.ASCII.GetString(data, offset, 4) == fourCc)
            {
                return true;
            }

            // Odd-sized payloads are followed by a padding byte.
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 4));
            offset += WebpConstants.ChunkHeaderSize + size + (size & 1);
        }

        return false;
    }
}
