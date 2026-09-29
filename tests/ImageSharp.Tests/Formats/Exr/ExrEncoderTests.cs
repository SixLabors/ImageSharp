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
        // The source's native and scaled vectors now carry the same numeric values. A half alpha
        // keeps this sensitive to association when the encoder writes EXR's associated channels.
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
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void FloatDefaultLoad_RetainsAssociatedColorAtZeroAlpha(ExrCompression compression)
    {
        using Image<RgbaVectorP> input = new(65, 1);
        RgbaVectorP pixel = new(2.500123F, -.50001F, .250001F, 0F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        using MemoryStream stream = new();
        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = compression });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<RgbaVectorP> output = Assert.IsType<Image<RgbaVectorP>>(decoded);

        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(pixel.ToVector4(), output[x, 0].ToVector4());
        }

        using MemoryStream second = new();
        decoded.Save(second, new ExrEncoder());

        second.Position = 0;
        using Image reloaded = Image.Load(second);
        Image<RgbaVectorP> reloadedPixels = Assert.IsType<Image<RgbaVectorP>>(reloaded);
        Assert.Equal(ExrPixelType.Float, reloaded.Metadata.GetExrMetadata().PixelType);
        Assert.Equal(pixel.ToVector4(), reloadedPixels[0, 0].ToVector4());
    }

    [Theory]
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void HalfDefaultLoad_RetainsAssociatedColorAtZeroAlpha(ExrCompression compression)
    {
        using Image<RgbaHalfP> input = new(65, 1);
        RgbaHalfP pixel = new(2.5F, -.5F, .25F, 0F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        using MemoryStream stream = new();
        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Half, Compression = compression });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<RgbaHalfP> output = Assert.IsType<Image<RgbaHalfP>>(decoded);

        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(pixel.ToVector4(), output[x, 0].ToVector4());
        }
    }

    [Theory]
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void FloatDefaultLoad_PreservesNonfiniteSamplesAndSignedZero(ExrCompression compression)
    {
        using Image<RgbaVectorP> input = new(65, 1);
        RgbaVectorP pixel = new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        using MemoryStream stream = new();
        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = compression });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<RgbaVectorP> output = Assert.IsType<Image<RgbaVectorP>>(decoded);

        for (int x = 0; x < output.Width; x++)
        {
            RgbaVectorP actual = output[x, 0];
            Assert.True(float.IsNaN(actual.R));
            Assert.True(float.IsPositiveInfinity(actual.G));
            Assert.True(float.IsNegativeInfinity(actual.B));
            Assert.Equal(BitConverter.SingleToInt32Bits(-0F), BitConverter.SingleToInt32Bits(actual.A));
        }
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
