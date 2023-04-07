// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);

            provider.Utility.TestName = DecodeBaselineJpegOutputName;
            image.CompareToReferenceOutput(
                GetImageComparer(provider),
                provider,
                appendPixelTypeToFileName: false);
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RunTest(providerDump, enforceDiscontiguousBuffers ? "Disco" : string.Empty);

        // RemoteExecutor.Invoke(
        //         RunTest,
        //         providerDump,
        //         enforceDiscontiguousBuffers ? "Disco" : string.Empty)
        //     .Dispose();
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
