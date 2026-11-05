// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;
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
        // The source pixel format limits conversion precision to half-precision storage for
        // every EXR sample type. These values follow the IEC equations with IEEE half rounding
        // of associated linear RGB before unassociation and sRGB compression.
        expected = new Vector4(.2499917932F, .4999439420F, .7499610878F, .5F);
        Assert.Equal(expected, output[0, 0].ToScaledVector4(), new ApproximateFloatComparer(1e-4F));
    }

    /// <summary>
    /// Verifies that float EXR conversion clears associated RGB when opacity is zero.
    /// </summary>
    /// <param name="compression">The lossless compression method.</param>
    [Theory]
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void FloatDefaultLoad_ClearsAssociatedColorAtZeroAlpha(ExrCompression compression)
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
        // Associated pixel packing multiplies RGB by opacity. Zero opacity must produce zero RGB.
        Vector4 expected = Vector4.Zero;

        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(expected, output[x, 0].ToVector4());
        }

        using MemoryStream second = new();
        decoded.Save(second, new ExrEncoder());

        second.Position = 0;
        using Image reloaded = Image.Load(second);
        Image<RgbaVectorP> reloadedPixels = Assert.IsType<Image<RgbaVectorP>>(reloaded);
        Assert.Equal(ExrPixelType.Float, reloaded.Metadata.GetExrMetadata().PixelType);
        Assert.Equal(expected, reloadedPixels[0, 0].ToVector4());
    }

    /// <summary>
    /// Verifies that half-precision EXR conversion clears associated RGB when opacity is zero.
    /// </summary>
    /// <param name="compression">The lossless compression method.</param>
    [Theory]
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void HalfDefaultLoad_ClearsAssociatedColorAtZeroAlpha(ExrCompression compression)
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
            Assert.Equal(Vector4.Zero, output[x, 0].ToVector4());
        }
    }

    /// <summary>
    /// EXR color conversion bounds nonfinite RGB and retains the sign of zero alpha.
    /// </summary>
    /// <param name="compression">The lossless compression method.</param>
    [Theory]
    [InlineData(ExrCompression.None)]
    [InlineData(ExrCompression.Zip)]
    [InlineData(ExrCompression.Zips)]
    public void FloatDefaultLoad_BoundsNonfiniteSamplesAndPreservesSignedZero(ExrCompression compression)
    {
        using Image<RgbaVectorP> input = new(65, 1);
        RgbaVectorP pixel = new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        input[input.Width - 1, 0] = new RgbaVectorP(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0F);

        using MemoryStream stream = new();
        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = compression });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<RgbaVectorP> output = Assert.IsType<Image<RgbaVectorP>>(decoded);

        // Encoding bounds the expanded nonfinite samples to the finite linear EXR limits.
        // Decoding compresses those linear endpoints into working-pixel RGB units.
        Vector4 expected = SRgbCompanding.Compress(new Vector4(-65504F, 65504F, -65504F, 1F));

        for (int x = 0; x < output.Width - 1; x++)
        {
            RgbaVectorP actual = output[x, 0];
            Assert.Equal(expected.X, actual.R);
            Assert.Equal(expected.Y, actual.G);
            Assert.Equal(expected.Z, actual.B);
            Assert.Equal(1F, actual.A);
        }

        // The last pixel exercises the scalar tail. Association clears its nonfinite RGB,
        // while the pixel API retains the sign of zero in the alpha component.
        RgbaVectorP transparent = output[output.Width - 1, 0];
        Assert.Equal(0F, transparent.R);
        Assert.Equal(0F, transparent.G);
        Assert.Equal(0F, transparent.B);
        Assert.Equal(BitConverter.SingleToInt32Bits(-0F), BitConverter.SingleToInt32Bits(transparent.A));
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

    /// <summary>
    /// Verifies raw ZIP fallback and native decoding with the stored float input.
    /// </summary>
    /// <param name="provider">The native float input whose ZIP block stores original pixels.</param>
    /// <param name="compression">The ZIP method used to write the stored pixels.</param>
    [Theory]
    [WithFile(TestImages.Exr.ZipUncompressedBlock, PixelTypes.RgbaVector, ExrCompression.Zip)]
    [WithFile(TestImages.Exr.ZipUncompressedBlock, PixelTypes.RgbaVector, ExrCompression.Zips)]
    public void ExrEncoder_StoredZipFallbackInput_MatchesNativeDecoder(TestImageProvider<RgbaVector> provider, ExrCompression compression)
    {
        using Image<RgbaVector> input = provider.GetImage(ExrDecoder.Instance);

        // A stored zlib stream adds framing bytes without reducing its input. The EXR compressor
        // must therefore return the original row, not a larger zlib payload. Borrow a decoded row
        // from the fixed input and retain its bytes independently before calling the compressor.
        Span<byte> row = MemoryMarshal.AsBytes(input.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0));
        byte[] original = row.ToArray();
        using MemoryStream fallback = new();
        using (ZipExrCompressor compressor = new(
            fallback, input.Configuration.MemoryAllocator, (uint)row.Length, (uint)row.Length, 1, input.Width, DeflateCompressionLevel.NoCompression))
        {
            Assert.Equal((uint)original.Length, compressor.CompressRowBlock(row, 1));
        }

        Assert.Equal(original, fallback.ToArray());

        using MemoryStream stream = new();
        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = compression });

        // Native decoding verifies that the emitted block framing is interoperable. Its integer
        // result has the same destination bounds as this explicit conversion of the stored input.
        // RGB passes through the sRGB and linear transfer curves, so this is not a raw float-bit round trip.
        stream.Position = 0;
        using Image<Rgba32> native = ReferenceDecoder.Decode<Rgba32>(DecoderOptions.Default, stream);
        using Image<Rgba32> expected = input.CloneAs<Rgba32>();
        ImageComparer.Exact.VerifySimilarity(expected, native);
    }

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
