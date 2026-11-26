// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public partial class JpegDecoderTests
{
    public const string DecodeProgressiveJpegOutputName = "DecodeProgressiveJpeg";

    [Theory]
    [WithFileCollection(nameof(ProgressiveTestJpegs), PixelTypes.Rgb24)]
    public void DecodeProgressiveJpeg<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider, appendPixelTypeToFileName: false);

        provider.Utility.TestName = DecodeProgressiveJpegOutputName;
        image.CompareToReferenceOutput(
            GetImageComparer(provider),
            provider,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCodingProgressive01, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Baseline.ArithmeticCodingProgressive02, PixelTypes.Rgb24)]
    public void DecodeProgressiveJpeg_WithArithmeticCoding<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0.004f), ReferenceDecoder);
    }

    /// <summary>
    /// Verifies that the arithmetic decoder starts the restart count again at the start of each scan, as libjpeg-turbo does.
    /// </summary>
    [Fact]
    public void DecodeProgressiveJpeg_WithArithmeticCodingAndRestartInterval_ResetsRestartCountForEachScan()
    {
        // The 200 x 200 image has 169 MCUs in an interleaved scan, and 625 blocks in a scan of the first component. A DRI segment with the interval 700
        // (0x02BC) comes after the SOI marker. No scan has 700 MCUs, so no scan has a restart marker. A count that continues through the scans reaches 700
        // in the second scan, and the decoder then looks for a restart marker that the scan does not have.
        byte[] original = TestFile.Create(TestImages.Jpeg.Baseline.ArithmeticCodingProgressive01).Bytes;
        byte[] data = [.. original.AsSpan(0, 2), 0xFF, 0xDD, 0x00, 0x04, 0x02, 0xBC, .. original.AsSpan(2)];

        using Image<Rgb24> expected = Image.Load<Rgb24>(original);
        using Image<Rgb24> actual = Image.Load<Rgb24>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Progressive.Progress, PixelTypes.Rgb24)]
    public void DecodeProgressiveJpeg_WithLimitedAllocatorBufferCapacity(TestImageProvider<Rgb24> provider)
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<Rgb24> provider =
                BasicSerializer.Deserialize<TestImageProvider<Rgb24>>(providerDump);

            provider.LimitAllocatorBufferCapacity().InBytesSqrt(200);

            using Image<Rgb24> image = provider.GetImage(JpegDecoder.Instance);
            image.DebugSave(provider, nonContiguousBuffersStr);

            provider.Utility.TestName = DecodeProgressiveJpegOutputName;
            image.CompareToReferenceOutput(
                GetImageComparer(provider),
                provider,
                appendPixelTypeToFileName: false);
        }

        string providerDump = BasicSerializer.Serialize(provider);

        RemoteExecutor.Invoke(
            RunTest,
            providerDump,
            "Disco").Dispose();
    }
}
