// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrEncoderTests
{
    protected static readonly IImageDecoder ReferenceDecoder = new MagickReferenceDecoder(ExrFormat.Instance);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithNoCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "NoCompression", compression: ExrCompression.None, imageDecoder: ExrDecoder.Instance);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipCompression", compression: ExrCompression.Zip, imageDecoder: ExrDecoder.Instance);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipsCompression", compression: ExrCompression.Zips, imageDecoder: ExrDecoder.Instance);

    protected static void TestExrEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        object testOutputDetails,
        ExrCompression compression = ExrCompression.None,
        bool useExactComparer = true,
        float compareTolerance = 0.001f,
        IImageDecoder imageDecoder = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ExrEncoder encoder = new()
        {
            Compression = compression,
        };

        // Does DebugSave & load reference CompareToReferenceInput():
        image.VerifyEncoder(
            provider,
            "exr",
            testOutputDetails: testOutputDetails,
            encoder: encoder,
            customComparer: useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
            referenceDecoder: imageDecoder ?? ReferenceDecoder);
    }
}
