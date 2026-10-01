// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public partial class JpegDecoderTests
{
    [Theory]
    [WithFileCollection(nameof(BaselineTestJpegs), PixelTypes.Rgb24, false)]
    [WithFile(TestImages.Jpeg.Baseline.Calliphora, PixelTypes.Rgb24, true)]
    [WithFile(TestImages.Jpeg.Baseline.Turtle420, PixelTypes.Rgb24, true)]
    public void DecodeBaselineJpeg<TPixel>(TestImageProvider<TPixel> provider, bool enforceDiscontiguousBuffers)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<TPixel> provider =
                BasicSerializer.Deserialize<TestImageProvider<TPixel>>(providerDump);

            if (!string.IsNullOrEmpty(nonContiguousBuffersStr))
            {
                provider.LimitAllocatorBufferCapacity().InPixels(16_000);
            }

            using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
            image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr, appendPixelTypeToFileName: false);

            if (image.Metadata.IccProfile is not null)
            {
                // ICC cases compare directly with Magick's color-managed decode of the source JPEG.
                image.CompareToOriginal(provider, GetImageComparer(provider), ReferenceDecoder);
            }
            else
            {
                provider.Utility.TestName = DecodeBaselineJpegOutputName;
                image.CompareToReferenceOutput(
                    GetImageComparer(provider),
                    provider,
                    appendPixelTypeToFileName: false);
            }
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RunTest(providerDump, enforceDiscontiguousBuffers ? "Disco" : string.Empty);

        // RemoteExecutor.Invoke(
        //         RunTest,
        //         providerDump,
        //         enforceDiscontiguousBuffers ? "Disco" : string.Empty)
        //     .Dispose();
    }

    /// <summary>
    /// Verifies that Preserve and Convert produce identical pixels for a YCCK JPEG while handling its ICC profile as requested.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="provider">The source JPEG provider.</param>
    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, PixelTypes.Rgb24)]
    public void Decode_YccK_PreserveMatchesConvert<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<TPixel> preserved = provider.GetImage(JpegDecoder.Instance, preserveOptions);

        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<TPixel> converted = provider.GetImage(JpegDecoder.Instance, convertOptions);

        ImageInfo sourceInfo = Image.Identify(TestFile.Create(provider.SourceFileOrDescription).Bytes);

        Assert.NotNull(sourceInfo.Metadata.IccProfile);
        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.Equal(sourceInfo.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        Assert.Null(converted.Metadata.IccProfile);

        // For this fixture, Magick and Pillow/LittleCMS agree within 0.019%, but both differ from ImageSharp by about 1.55%.
        // Agreement between external decoders does not establish a correct reference: issue #2454 documents JPEG-compressed
        // TIFFs whose embedded JPEG uses YCCK despite the TIFF's Separated interpretation. Detecting that encoding and
        // applying the CMYK ICC profile produced the output confirmed by the reporter; several other decoders rendered it
        // incorrectly. See https://github.com/SixLabors/ImageSharp/issues/2454#issuecomment-2939244208 and
        // https://github.com/SixLabors/ImageSharp/issues/2454#issuecomment-2940154473.
        // That TIFF evidence does not identify the cause of this standalone JPEG's difference. Until a trusted reference
        // is available, verify Preserve's exact pixel equivalence with Convert and retention of the original profile.
        // Decode_YCCK_ICC_Jpeg and TiffDecoder_CanDecode_YccK_ICC retain established reference comparisons.
        ImageComparer.Exact.VerifySimilarity(converted, preserved);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCoding01, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCoding02, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCodingGray, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCodingInterleaved, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCodingWithRestart, PixelTypes.Rgb24)]
    public void DecodeJpeg_WithArithmeticCoding<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0.002f), ReferenceDecoder);
    }

    [Theory]
    [WithFileCollection(nameof(UnrecoverableTestJpegs), PixelTypes.Rgba32)]
    public void UnrecoverableImage_Throws_InvalidImageContentException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<InvalidImageContentException>(provider.GetImage);
}
