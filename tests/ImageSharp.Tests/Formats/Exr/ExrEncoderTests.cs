// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
    [InlineData(null, ExrPixelType.Half)]
    [InlineData(ExrPixelType.Float, ExrPixelType.Float)]
    [InlineData(ExrPixelType.Half, ExrPixelType.Half)]
    [InlineData(ExrPixelType.UnsignedInt, ExrPixelType.UnsignedInt)]
    public void EncoderOptions_SetPixelType_Works(ExrPixelType? pixelType, ExrPixelType? expectedPixelType)
    {
        // arrange
        ExrEncoder exrEncoder = new() { PixelType = pixelType };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, exrEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ExrMetadata exrMetaData = output.Metadata.GetExrMetadata();
        Assert.Equal(expectedPixelType, exrMetaData.PixelType);
    }

    [Theory]
    [InlineData(ExrPixelType.Half)]
    [InlineData(ExrPixelType.Float)]
    [InlineData(ExrPixelType.UnsignedInt)]
    public void Encode_PixelFormatWithNonUnitNativeRange_WritesScaledValues(ExrPixelType pixelType)
    {
        // arrange
        // HalfVector4 stores the scaled range [0, 1] as the native range [-65504, 65504], so its native and scaled
        // vectors differ. The alpha of 0.5 makes the test sensitive to the alpha channel too: a wrong alpha value
        // larger than 1 would otherwise be hidden by the clamp in the decoder.
        Vector4 expected = new(0.25F, 0.5F, 0.75F, 0.5F);
        ExrEncoder exrEncoder = new() { PixelType = pixelType };
        using Image<HalfVector4> input = new(2, 2, HalfVector4.FromScaledVector4(expected));
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, exrEncoder);

        // assert
        memStream.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(memStream);
        Assert.Equal(expected, output[0, 0].ToScaledVector4(), new ApproximateFloatComparer(1e-4F));
    }

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithNoCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "NoCompression", compression: ExrCompression.None);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipCompression", compression: ExrCompression.Zip);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipsCompression", compression: ExrCompression.Zips);

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
