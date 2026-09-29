// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffEncoderTests : TiffEncoderBaseTester
{
    [Fact]
    public void TiffEncoderDefaultInstanceHasQuantizer() => Assert.NotNull(new TiffEncoder().Quantizer);

    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.PackBits)]
    [InlineData(TiffCompression.Deflate)]
    [InlineData(TiffCompression.Lzw)]
    public void FloatSingleComponent_RoundTripsWithoutChangingIntensity(TiffCompression compression)
    {
        using Image<HalfSingle> input = new(65, 1);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = new HalfSingle(x % 2 == 0 ? 2.5F : -.5F);
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, Compression = compression });

        stream.Position = 0;
        using Image<HalfSingle> output = Image.Load<HalfSingle>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffSampleFormat.Float, metadata.SampleFormat);
        Assert.Equal(TiffBitsPerPixel.Bit32, metadata.BitsPerPixel);
        Assert.Equal((byte)1, metadata.BitsPerSample.Channels);
        Assert.Equal((ushort)32, metadata.BitsPerSample.Channel0);
        Assert.Equal(compression, metadata.Compression);

        stream.Position = 0;
        ImageInfo identified = Image.Identify(stream);
        Assert.Equal(TiffSampleFormat.Float, identified.Metadata.GetTiffMetadata().SampleFormat);
        Assert.Equal(TiffSampleFormat.Float, identified.FrameMetadataCollection[0].GetTiffMetadata().SampleFormat);

        // The row crosses the writer's 64-pixel block boundary, so both blocks must retain the samples.
        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(input[x, 0].ToSingle(), output[x, 0].ToSingle());
        }

        stream.Position = 0;
        using Image defaultOutput = Image.Load(stream);
        Image<RgbaVector> defaultPixels = Assert.IsType<Image<RgbaVector>>(defaultOutput);
        Assert.Equal(2.5F, defaultPixels[0, 0].R);
    }

    [Theory]
    [InlineData(TiffCompression.None, TiffPredictor.None)]
    [InlineData(TiffCompression.PackBits, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.None)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.Horizontal)]
    public void Rgb48_RoundTrips16BitSamples(TiffCompression compression, TiffPredictor predictor)
    {
        using Image<Rgb48> input = new(65, 2);

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                input[x, y] = new Rgb48((ushort)(0x1201 + x + y), (ushort)(0x3402 + (x * 3)), (ushort)(0x5603 + (y * 7)));
            }
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit48, Compression = compression, HorizontalPredictor = predictor });

        stream.Position = 0;
        using Image<Rgb48> output = Image.Load<Rgb48>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffBitsPerPixel.Bit48, metadata.BitsPerPixel);
        Assert.Equal(new TiffBitsPerSample(16, 16, 16), metadata.BitsPerSample);
        Assert.Equal(TiffSampleFormat.UnsignedInteger, metadata.SampleFormat);
        Assert.Equal(compression, metadata.Compression);
        Assert.Equal(predictor, metadata.Predictor);

        // Distinct low bytes expose any conversion through an 8-bit pixel format.
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }

        stream.Position = 0;
        using Image defaultOutput = Image.Load(stream);
        Image<Rgb48> defaultPixels = Assert.IsType<Image<Rgb48>>(defaultOutput);
        Assert.Equal(input[64, 1], defaultPixels[64, 1]);
    }

    [Theory]
    [InlineData(TiffCompression.None, TiffPredictor.None)]
    [InlineData(TiffCompression.PackBits, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.None)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.Horizontal)]
    public void Rgba64_RoundTrips16BitSamples(TiffCompression compression, TiffPredictor predictor)
    {
        using Image<Rgba64> input = new(65, 2);

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                input[x, y] = new Rgba64((ushort)(0x1201 + x + y), (ushort)(0x3402 + (x * 3)), (ushort)(0x5603 + (y * 7)), (ushort)(0x7804 + x));
            }
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit64, Compression = compression, HorizontalPredictor = predictor });

        stream.Position = 0;
        using Image<Rgba64> output = Image.Load<Rgba64>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffBitsPerPixel.Bit64, metadata.BitsPerPixel);
        Assert.Equal(new TiffBitsPerSample(16, 16, 16, 16), metadata.BitsPerSample);
        Assert.Equal(TiffSampleFormat.UnsignedInteger, metadata.SampleFormat);
        Assert.Equal(TiffExtraSampleType.UnassociatedAlphaData, metadata.ExtraSampleType);
        Assert.Equal(compression, metadata.Compression);
        Assert.Equal(predictor, metadata.Predictor);

        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }

        stream.Position = 0;
        using Image defaultOutput = Image.Load(stream);
        Image<Rgba64> defaultPixels = Assert.IsType<Image<Rgba64>>(defaultOutput);
        Assert.Equal(input[64, 1], defaultPixels[64, 1]);
    }

    [Theory]
    [WithFile(FlowerRgb161616Contiguous, PixelTypes.Rgb48, TiffBitsPerPixel.Bit48)]
    [WithFile(FlowerRgb161616ContiguousLittleEndian, PixelTypes.Rgb48, TiffBitsPerPixel.Bit48)]
    [WithFile(FlowerRgb161616PredictorBigEndian, PixelTypes.Rgb48, TiffBitsPerPixel.Bit48)]
    [WithFile(FlowerRgb161616PredictorLittleEndian, PixelTypes.Rgb48, TiffBitsPerPixel.Bit48)]
    [WithFile(Rgba16BitUnassociatedAlphaBigEndian, PixelTypes.Rgba64, TiffBitsPerPixel.Bit64)]
    [WithFile(Rgba16BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba64, TiffBitsPerPixel.Bit64)]
    [WithFile(Rgba16BitUnassociatedAlphaBigEndianWithPredictor, PixelTypes.Rgba64, TiffBitsPerPixel.Bit64)]
    [WithFile(Rgba16BitUnassociatedAlphaLittleEndianWithPredictor, PixelTypes.Rgba64, TiffBitsPerPixel.Bit64)]
    public void SixteenBitReferenceImages_RoundTrip<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);

        // Check the independent source decode first, so a decoder error cannot make
        // the subsequent encoder comparison appear correct by repeating that error.
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);

        TiffEncoder encoder = new()
        {
            BitsPerPixel = bitsPerPixel,
            Compression = TiffCompression.Deflate,
            HorizontalPredictor = TiffPredictor.Horizontal
        };

        image.VerifyEncoder(provider, "tiff", bitsPerPixel, encoder, ImageComparer.Exact, referenceDecoder: ReferenceDecoder);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit96, TiffCompression.None)]
    [InlineData(TiffBitsPerPixel.Bit96, TiffCompression.PackBits)]
    [InlineData(TiffBitsPerPixel.Bit96, TiffCompression.Deflate)]
    [InlineData(TiffBitsPerPixel.Bit96, TiffCompression.Lzw)]
    [InlineData(TiffBitsPerPixel.Bit128, TiffCompression.None)]
    [InlineData(TiffBitsPerPixel.Bit128, TiffCompression.PackBits)]
    [InlineData(TiffBitsPerPixel.Bit128, TiffCompression.Deflate)]
    [InlineData(TiffBitsPerPixel.Bit128, TiffCompression.Lzw)]
    public void FloatColor_RoundTripsSamplesAndLayout(TiffBitsPerPixel bitsPerPixel, TiffCompression compression)
    {
        using Image<RgbaVector> input = new(65, 1);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = new RgbaVector(2.500123F + (x * .25F), -.50001F, .250001F, .5F);
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, BitsPerPixel = bitsPerPixel, Compression = compression });

        stream.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffSampleFormat.Float, metadata.SampleFormat);
        Assert.Equal(bitsPerPixel, metadata.BitsPerPixel);
        Assert.Equal((byte)((int)bitsPerPixel / 32), metadata.BitsPerSample.Channels);
        Assert.Equal(TiffPhotometricInterpretation.Rgb, metadata.PhotometricInterpretation);
        Assert.Equal(compression, metadata.Compression);
        Assert.Equal(bitsPerPixel == TiffBitsPerPixel.Bit128 ? TiffExtraSampleType.UnassociatedAlphaData : null, metadata.ExtraSampleType);

        for (int x = 0; x < input.Width; x++)
        {
            RgbaVector actual = output[x, 0];
            Assert.Equal(2.500123F + (x * .25F), actual.R);
            Assert.Equal(-.50001F, actual.G);
            Assert.Equal(.250001F, actual.B);
            Assert.Equal(bitsPerPixel == TiffBitsPerPixel.Bit128 ? .5F : 1F, actual.A);
        }

        stream.Position = 0;
        using Image defaultOutput = Image.Load(stream);
        Image<RgbaVector> defaultPixels = Assert.IsType<Image<RgbaVector>>(defaultOutput);
        Assert.Equal(2.500123F, defaultPixels[0, 0].R);
    }

    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.PackBits)]
    [InlineData(TiffCompression.Deflate)]
    [InlineData(TiffCompression.Lzw)]
    public void FloatAssociatedColor_RetainsStoredColorAtZeroAlpha(TiffCompression compression)
    {
        using Image<RgbaVectorP> input = new(65, 1);
        RgbaVectorP pixel = new(2.500123F, -.50001F, .250001F, 0F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, Compression = compression });

        stream.Position = 0;
        using Image<RgbaVectorP> output = Image.Load<RgbaVectorP>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffSampleFormat.Float, metadata.SampleFormat);
        Assert.Equal(TiffBitsPerPixel.Bit128, metadata.BitsPerPixel);
        Assert.Equal(TiffExtraSampleType.AssociatedAlphaData, metadata.ExtraSampleType);
        Assert.Equal(compression, metadata.Compression);

        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(pixel.ToVector4(), output[x, 0].ToVector4());
        }

        stream.Position = 0;
        using Image defaultOutput = Image.Load(stream);
        Image<RgbaVectorP> defaultPixels = Assert.IsType<Image<RgbaVectorP>>(defaultOutput);
        Assert.Equal(pixel.ToVector4(), defaultPixels[0, 0].ToVector4());
    }

    [Fact]
    public void FloatTiff_DefaultReencodeRetainsFloatSamples()
    {
        using Image<RgbaVector> input = new(65, 1);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = new RgbaVector(2.5F + (x * .25F), -.5F, .25F, 1F);
        }

        using MemoryStream first = new();
        input.Save(first, new TiffEncoder { SampleFormat = TiffSampleFormat.Float });

        first.Position = 0;
        using Image decoded = Image.Load(first);
        Assert.IsType<Image<RgbaVector>>(decoded);

        using MemoryStream second = new();
        decoded.Save(second, new TiffEncoder());

        second.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(second);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffSampleFormat.Float, metadata.SampleFormat);
        Assert.Equal(TiffBitsPerPixel.Bit128, metadata.BitsPerPixel);

        for (int x = 0; x < input.Width; x++)
        {
            Assert.Equal(input[x, 0], output[x, 0]);
        }
    }

    [Fact]
    public void FloatTiff_DefaultLoadPreservesNonfiniteSamplesAndSignedZero()
    {
        using Image<RgbaVectorP> input = new(65, 1);
        RgbaVectorP pixel = new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0F);

        for (int x = 0; x < input.Width; x++)
        {
            input[x, 0] = pixel;
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, Compression = TiffCompression.Deflate });

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
    [InlineData(null, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.WhiteIsZero, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffBitsPerPixel.Bit16)]
    //// Unsupported TiffPhotometricInterpretation should default to 24 bits
    [InlineData(TiffPhotometricInterpretation.CieLab, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.ColorFilterArray, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.ItuLab, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.LinearRaw, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.Separated, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.TransparencyMask, TiffBitsPerPixel.Bit24)]
    public void EncoderOptions_SetPhotometricInterpretation_Works(TiffPhotometricInterpretation? photometricInterpretation, TiffBitsPerPixel expectedBitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new() { BitsPerPixel = expectedBitsPerPixel, PhotometricInterpretation = photometricInterpretation };
        using Image input = expectedBitsPerPixel is TiffBitsPerPixel.Bit16
            ? new Image<L16>(10, 10)
            : new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, frameMetaData.BitsPerPixel);
        Assert.Equal(TiffCompression.None, frameMetaData.Compression);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit24)]
    [InlineData(TiffBitsPerPixel.Bit16)]
    [InlineData(TiffBitsPerPixel.Bit8)]
    [InlineData(TiffBitsPerPixel.Bit4)]
    [InlineData(TiffBitsPerPixel.Bit1)]
    public void EncoderOptions_SetBitPerPixel_Works(TiffBitsPerPixel bitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new() { BitsPerPixel = bitsPerPixel };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(bitsPerPixel, frameMetaData.BitsPerPixel);
        Assert.Equal(TiffCompression.None, frameMetaData.Compression);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit12)]
    [InlineData(TiffBitsPerPixel.Bit10)]
    [InlineData(TiffBitsPerPixel.Bit6)]
    public void EncoderOptions_UnsupportedBitPerPixel_DefaultTo24Bits(TiffBitsPerPixel bitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { BitsPerPixel = bitsPerPixel };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit24, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit30, TiffCompression.None)]
    [InlineData(TiffBitsPerPixel.Bit36, TiffCompression.None)]
    [InlineData(TiffBitsPerPixel.Bit42, TiffCompression.None)]
    [InlineData(TiffBitsPerPixel.Bit30, TiffCompression.Jpeg)]
    [InlineData(TiffBitsPerPixel.Bit36, TiffCompression.Jpeg)]
    [InlineData(TiffBitsPerPixel.Bit42, TiffCompression.Jpeg)]
    public void EncoderOptions_UnsupportedColorDepth_Uses48Bits(TiffBitsPerPixel bitsPerPixel, TiffCompression compression)
    {
        // Nonzero low bytes reveal a fallback through 8-bit samples.
        Rgb48 expected = new(0x1201, 0x3402, 0x5603);
        using Image<Rgb48> input = new(1, 1);
        input[0, 0] = expected;
        using MemoryStream stream = new();

        input.Save(stream, new TiffEncoder { BitsPerPixel = bitsPerPixel, Compression = compression });

        stream.Position = 0;
        using Image<Rgb48> output = Image.Load<Rgb48>(stream);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffBitsPerPixel.Bit48, metadata.BitsPerPixel);
        Assert.Equal(new TiffBitsPerSample(16, 16, 16), metadata.BitsPerSample);
        Assert.Equal(compression == TiffCompression.Jpeg ? TiffCompression.Deflate : compression, metadata.Compression);
        Assert.Equal(expected, output[0, 0]);
    }

    [Theory]
    [InlineData(TiffCompression.None, TiffPredictor.None)]
    [InlineData(TiffCompression.PackBits, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.None)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Jpeg, TiffPredictor.None)]
    public void UnsignedRgb96_RoundTripsEverySampleBit(TiffCompression compression, TiffPredictor predictor)
    {
        // 2^24 + 1 is the first integer a float cannot represent exactly.
        // The second value sets the unsigned high bit and keeps its low bit set.
        const uint FirstInexactFloatSample = 0x01000001U;
        const uint HighUnsignedSample = 0x80000001U;

        // The 65-pixel rows cross SIMD block boundaries and check that the predictor
        // restarts on the second row. Decreasing from uint.MaxValue also checks the
        // upper bound while adjacent samples differ in bits Vector4 would lose.
        using Image<Rgb96> input = new(65, 2);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                input[x, y] = new Rgb96(FirstInexactFloatSample + (uint)x, HighUnsignedSample + (uint)y, uint.MaxValue - (uint)x);
            }
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.UnsignedInteger, BitsPerPixel = TiffBitsPerPixel.Bit96, Compression = compression, HorizontalPredictor = predictor });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<Rgb96> output = Assert.IsType<Image<Rgb96>>(decoded);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffBitsPerPixel.Bit96, metadata.BitsPerPixel);
        Assert.Equal(new TiffBitsPerSample(32, 32, 32), metadata.BitsPerSample);
        Assert.Equal(TiffSampleFormat.UnsignedInteger, metadata.SampleFormat);
        Assert.Equal(compression == TiffCompression.Jpeg ? TiffCompression.Deflate : compression, metadata.Compression);
        Assert.Equal(predictor, metadata.Predictor);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(TiffCompression.None, TiffPredictor.None)]
    [InlineData(TiffCompression.PackBits, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.None)]
    [InlineData(TiffCompression.Deflate, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.None)]
    [InlineData(TiffCompression.Lzw, TiffPredictor.Horizontal)]
    [InlineData(TiffCompression.Jpeg, TiffPredictor.None)]
    public void UnsignedRgba128_RoundTripsEverySampleBit(TiffCompression compression, TiffPredictor predictor)
    {
        // These values exercise the first integer a float cannot represent,
        // the unsigned high bit, and a non-opaque alpha with a significant low bit.
        const uint FirstInexactFloatSample = 0x01000001U;
        const uint HighUnsignedSample = 0x80000001U;
        const uint NonOpaqueAlphaSample = 0x40000001U;

        // Cross SIMD block and row boundaries and decrease from uint.MaxValue.
        // The first pixel in each row has nonzero color with zero alpha, which
        // encoding must retain.
        using Image<Rgba128> input = new(65, 2);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                input[x, y] = new Rgba128(FirstInexactFloatSample + (uint)x, HighUnsignedSample + (uint)y, uint.MaxValue - (uint)x, x == 0 ? 0U : NonOpaqueAlphaSample + (uint)x);
            }
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.UnsignedInteger, BitsPerPixel = TiffBitsPerPixel.Bit128, Compression = compression, HorizontalPredictor = predictor });

        stream.Position = 0;
        using Image decoded = Image.Load(stream);
        Image<Rgba128> output = Assert.IsType<Image<Rgba128>>(decoded);
        TiffFrameMetadata metadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.Equal(TiffBitsPerPixel.Bit128, metadata.BitsPerPixel);
        Assert.Equal(new TiffBitsPerSample(32, 32, 32, 32), metadata.BitsPerSample);
        Assert.Equal(TiffSampleFormat.UnsignedInteger, metadata.SampleFormat);
        Assert.Equal(TiffExtraSampleType.UnassociatedAlphaData, metadata.ExtraSampleType);
        Assert.Equal(compression == TiffCompression.Jpeg ? TiffCompression.Deflate : compression, metadata.Compression);
        Assert.Equal(predictor, metadata.Predictor);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(TiledRgb96BitLittleEndianDeflateCompressedWithPredictor)]
    [InlineData(TiledRgb96BitBigEndianDeflateCompressedWithPredictor)]
    [InlineData(TiledRgb96BitLittleEndianLzwCompressedWithPredictor)]
    [InlineData(TiledRgb96BitBigEndianLzwCompressedWithPredictor)]
    public void UnsignedRgb96_RealReferenceImage_RoundTrips(string path)
    {
        using Image<Rgb96> input = Image.Load<Rgb96>(TestFile.GetInputFileFullPath(path));

        // These are the sample words at two pixels in libtiff's uncompressed output
        // of the source tiles. Hex keeps each stored byte visible.
        Assert.Equal(new Rgb96(0x0D0D0D0DU, 0x0D0D0D0DU, 0x0F0F0F0FU), input[0, 0]);
        Assert.Equal(new Rgb96(0xB1B1B1B1U, 0xA9A9A9A9U, 0x9E9E9E9EU), input[65, 97]);

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit96, Compression = TiffCompression.Deflate });

        stream.Position = 0;
        using Image<Rgb96> output = Image.Load<Rgb96>(stream);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(TiledRgba128BitLittleEndianDeflateCompressedWithPredictor)]
    [InlineData(TiledRgba128BitBigEndianDeflateCompressedWithPredictor)]
    [InlineData(TiledRgba128BitLittleEndianLzwCompressedWithPredictor)]
    [InlineData(TiledRgba128BitBigEndianLzwCompressedWithPredictor)]
    public void UnsignedRgba128_RealReferenceImage_RoundTrips(string path)
    {
        using Image<Rgba128> input = Image.Load<Rgba128>(TestFile.GetInputFileFullPath(path));

        // These are the sample words at two pixels in libtiff's uncompressed output
        // of the source tiles. Hex keeps each stored byte visible.
        Assert.Equal(new Rgba128(0x0D0D0D0DU, 0x0D0D0D0DU, 0x0F0F0F0FU, uint.MaxValue), input[0, 0]);
        Assert.Equal(new Rgba128(0xB1B1B1B1U, 0xA9A9A9A9U, 0x9E9E9E9EU, uint.MaxValue), input[65, 97]);

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit128, Compression = TiffCompression.Deflate });

        stream.Position = 0;
        using Image<Rgba128> output = Image.Load<Rgba128>(stream);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(FlowerRgb323232Planar)]
    [InlineData(FlowerRgb323232PlanarLittleEndian)]
    public void UnsignedRgb96_PlanarReferenceImage_RoundTrips(string path)
    {
        using Image<Rgb96> input = Image.Load<Rgb96>(TestFile.GetInputFileFullPath(path));

        // These are the 32-bit sample words at two pixels in the uncompressed
        // planar TIFF. Hex keeps each stored byte visible for byte-order checks.
        Assert.Equal(new Rgb96(0x58CC576BU, 0x58A64DD6U, 0x47713036U), input[0, 0]);
        Assert.Equal(new Rgb96(0x44B8137EU, 0x4650DF9BU, 0x3C3B4613U), input[72, 42]);

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit96, Compression = TiffCompression.Deflate });

        stream.Position = 0;
        using Image<Rgb96> output = Image.Load<Rgb96>(stream);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaLittleEndian)]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaBigEndian)]
    public void UnsignedRgba128_PlanarReferenceImage_RoundTrips(string path)
    {
        using Image<Rgba128> input = Image.Load<Rgba128>(TestFile.GetInputFileFullPath(path));

        // These hex values are the exact 32-bit sample words at the named pixels
        // in the uncompressed planar TIFF, with each byte visible. The first
        // pixel has stored color at zero alpha; the second checks nonzero alpha.
        Assert.Equal(new Rgba128(0xB2B2B2B2U, 0xB2B2B2B2U, 0xFEFEFEFEU, 0U), input[78, 7]);
        Assert.Equal(new Rgba128(0xD2D2D2D2U, 0xA0A0A0A0U, 0xA0A0A0A0U, 0xF7F7F7F7U), input[140, 105]);

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit128, Compression = TiffCompression.Deflate });

        stream.Position = 0;
        using Image<Rgba128> output = Image.Load<Rgba128>(stream);
        for (int y = 0; y < input.Height; y++)
        {
            for (int x = 0; x < input.Width; x++)
            {
                Assert.Equal(input[x, y], output[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Ccitt1D)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.CcittGroup3Fax)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.CcittGroup4Fax)]
    public void EncoderOptions_WithInvalidCompressionAndPixelTypeCombination_DefaultsToRgb(TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { PhotometricInterpretation = photometricInterpretation, Compression = compression };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit24, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [InlineData(null, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffBitsPerPixel.Bit16, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
    [InlineData(null, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits, TiffBitsPerPixel.Bit16, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
    [InlineData(null, TiffCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffBitsPerPixel.Bit16, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup3Fax)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup4Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup4Fax)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Ccitt1D, TiffBitsPerPixel.Bit1, TiffCompression.Ccitt1D)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.ItuTRecT43, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.ItuTRecT82, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Jpeg, TiffBitsPerPixel.Bit24, TiffCompression.Jpeg)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.OldDeflate, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.OldJpeg, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    public void EncoderOptions_SetPhotometricInterpretationAndCompression_Works(
        TiffPhotometricInterpretation? photometricInterpretation,
        TiffCompression compression,
        TiffBitsPerPixel expectedBitsPerPixel,
        TiffCompression expectedCompression)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        {
            BitsPerPixel = expectedBitsPerPixel,
            PhotometricInterpretation = photometricInterpretation,
            Compression = compression
        };
        using Image input = expectedBitsPerPixel is TiffBitsPerPixel.Bit16
            ? new Image<L16>(10, 10)
            : new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata rootFrameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, rootFrameMetaData.BitsPerPixel);
        Assert.Equal(expectedCompression, rootFrameMetaData.Compression);
    }

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit1)]
    [WithFile(GrayscaleUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    [WithFile(GrayscaleUncompressed16Bit, PixelTypes.L16, TiffBitsPerPixel.Bit16)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit24)]
    [WithFile(Rgb4BitPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit4)]
    [WithFile(RgbPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    public void TiffEncoder_PreservesBitsPerPixel<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel expectedBitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.None)]
    [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32, TiffCompression.Lzw)]
    [WithFile(RgbDeflate, PixelTypes.Rgba32, TiffCompression.Deflate)]
    [WithFile(RgbPackbits, PixelTypes.Rgba32, TiffCompression.PackBits)]
    public void TiffEncoder_PreservesCompression<TPixel>(TestImageProvider<TPixel> provider, TiffCompression expectedCompression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        Assert.Equal(expectedCompression, output.Frames.RootFrame.Metadata.GetTiffMetadata().Compression);
    }

    [Theory]
    [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32, TiffPredictor.None)]
    [WithFile(RgbLzwPredictor, PixelTypes.Rgba32, TiffPredictor.Horizontal)]
    [WithFile(RgbDeflate, PixelTypes.Rgba32, TiffPredictor.None)]
    [WithFile(RgbDeflatePredictor, PixelTypes.Rgba32, TiffPredictor.Horizontal)]
    public void TiffEncoder_PreservesPredictor<TPixel>(TestImageProvider<TPixel> provider, TiffPredictor expectedPredictor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedPredictor, frameMetadata.Predictor);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2297
    [Fact]
    public void TiffEncoder_WritesIfdOffsetAtWordBoundary()
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using MemoryStream memStream = new();
        using Image<Rgba32> image = new(1, 1);
        byte[] expectedIfdOffsetBytes = [12, 0];

        // act
        image.Save(memStream, tiffEncoder);

        // assert
        byte[] imageBytes = memStream.ToArray();
        Assert.Equal(imageBytes[4], expectedIfdOffsetBytes[0]);
        Assert.Equal(imageBytes[5], expectedIfdOffsetBytes[1]);
    }

    [Theory]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.CcittGroup4Fax, TiffCompression.CcittGroup4Fax)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffCompression.CcittGroup4Fax, TiffCompression.CcittGroup4Fax)]
    [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
    public void TiffEncoder_EncodesWithCorrectBiColorModeCompression<TPixel>(TestImageProvider<TPixel> provider, TiffCompression compression, TiffCompression expectedCompression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder encoder = new() { Compression = compression, BitsPerPixel = TiffBitsPerPixel.Bit1 };
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit1, frameMetaData.BitsPerPixel);
        Assert.Equal(expectedCompression, frameMetaData.Compression);
    }

    [Theory]
    [WithFile(MultiFrameMipMap, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodesMultiFrameMipMap<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        Assert.Equal(7, image.Frames.Count);

        using MemoryStream memStream = new();
        image.SaveAsTiff(memStream);

        memStream.Position = 0;
        using Image<TPixel> output = Image.Load<TPixel>(memStream);

        Assert.Equal(image.Size, output.Size);
        Assert.Equal(image.Frames.Count, output.Frames.Count);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            TiffFrameMetadata inputMetadata = image.Frames[i].Metadata.GetTiffMetadata();
            TiffFrameMetadata outputMetadata = output.Frames[i].Metadata.GetTiffMetadata();

            Assert.Equal(inputMetadata.EncodingWidth, outputMetadata.EncodingWidth);
            Assert.Equal(inputMetadata.EncodingHeight, outputMetadata.EncodingHeight);
        }
    }

    [Theory]
    [WithFile(MultiFrameMipMap, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodesMultiFrameMipMap_WithScaling<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        Assert.Equal(7, image.Frames.Count);

        Size size = image.Size;

        List<Size> encodedDimensions = [];
        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            TiffFrameMetadata metadata = frame.Metadata.GetTiffMetadata();
            encodedDimensions.Add(new Size(metadata.EncodingWidth, metadata.EncodingHeight));
        }

        const int scale = 2;
        image.Mutate(x => x.Resize(image.Width / scale, image.Height / scale));

        using MemoryStream memStream = new();
        image.SaveAsTiff(memStream);

        memStream.Position = 0;
        using Image<TPixel> output = Image.Load<TPixel>(memStream);

        Assert.Equal(image.Size, output.Size);
        Assert.Equal(image.Frames.Count, output.Frames.Count);

        // The encoded dimensions should automatically be scaled down by the
        // horizontal and vertical scaling factors.
        float ratioX = output.Width / (float)size.Width;
        float ratioY = output.Height / (float)size.Height;

        for (int i = 0; i < image.Frames.Count; i++)
        {
            TiffFrameMetadata inputMetadata = image.Frames[i].Metadata.GetTiffMetadata();
            TiffFrameMetadata outputMetadata = output.Frames[i].Metadata.GetTiffMetadata();

            int expectedWidth = (int)MathF.Ceiling(encodedDimensions[i].Width * ratioX);
            int expectedHeight = (int)MathF.Ceiling(encodedDimensions[i].Height * ratioY);

            Assert.Equal(expectedWidth, inputMetadata.EncodingWidth);
            Assert.Equal(expectedHeight, inputMetadata.EncodingHeight);
            Assert.Equal(inputMetadata.EncodingWidth, outputMetadata.EncodingWidth);
            Assert.Equal(inputMetadata.EncodingHeight, outputMetadata.EncodingHeight);
        }
    }

    // This makes sure, that when decoding a planar tiff, the planar configuration is not carried over to the encoded image.
    [Theory]
    [WithFile(FlowerRgb444Planar, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodePlanar_AndReload_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithJpegCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Jpeg, useExactComparer: false, compareTolerance: 0.012f);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Rgb4BitPalette, PixelTypes.Rgba32)]
    [WithFile(Flower4BitPalette, PixelTypes.Rgba32)]
    [WithFile(Flower4BitPaletteGray, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_With4Bit_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit4, TiffPhotometricInterpretation.PaletteColor, useExactComparer: false, compareTolerance: 0.003f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.PackBits, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithDeflateCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithDeflateCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.CcittGroup3Fax);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax);

    /// <summary>
    /// CCITT row framing must fit even when each row contains only one pixel.
    /// </summary>
    /// <param name="width">The image width.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void TiffEncoder_EncodeNarrowCcittGroup3Fax_Works(int width)
    {
        using Image<L8> image = new(width, 2000);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new L8((byte)(((x + y) & 1) == 0 ? 255 : 0));
            }
        }

        TiffFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetTiffMetadata();
        metadata.BitsPerPixel = TiffBitsPerPixel.Bit1;
        metadata.Compression = TiffCompression.CcittGroup3Fax;

        using MemoryStream output = new();
        image.Save(output, new TiffEncoder());

        output.Position = 0;
        using Image<L8> decoded = Image.Load<L8>(output);
        Assert.Equal(image.Size, decoded.Size);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Assert.Equal(image[x, y], decoded[x, y]);
            }
        }
    }

    [Theory]
    [WithFile(Issues2255, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_WithoutSpecifyingBitPerPixel_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, null, null, TiffCompression.CcittGroup3Fax, useExactComparer: false, compareTolerance: 0.025f);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup4FaxCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.CcittGroup4Fax);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup4FaxCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup4Fax);

    /// <summary>
    /// Re-encoding a one-pixel Group 4 image must retain its pixel and fit the end-of-block code.
    /// </summary>
    [Fact]
    public void TiffEncoder_ReencodeNarrowCcittGroup4Fax_Works()
    {
        byte[] data = Convert.FromBase64String(
            "SUkqAAgAAAAJAAABAwABAAAAAQAAAAEBAwABAAAAAQAAAAIBAwABAAAAAQAAAAMBAwABAAAABAAAAAYBAwABAAAAAAAAABEBBAABAAAA" +
            "egAAABUBAwABAAAAAQAAABYBBAABAAAAAQAAABcBBAABAAAABAAAAAAAAACACACA");

        using Image<L8> image = Image.Load<L8>(data);
        using MemoryStream output = new();
        image.Save(output, new TiffEncoder());

        output.Position = 0;
        using Image<L8> decoded = Image.Load<L8>(output);
        Assert.Equal(new Size(1, 1), decoded.Size);
        Assert.Equal(image[0, 0], decoded[0, 0]);
    }

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.Ccitt1D);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Ccitt1D);

    [Theory]
    [WithFile(Issue2909, PixelTypes.Rgba32)]
    public void TiffEncoder_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, null, TiffCompression.Lzw, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(Issue2909, PixelTypes.Rgba32)]
    public void TiffEncoder_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, null, TiffCompression.Deflate, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits)]
    [WithFile(GrayscaleUncompressed16Bit, PixelTypes.L16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate)]
    [WithFile(RgbUncompressed, PixelTypes.Rgb24, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    [WithFile(RgbUncompressed, PixelTypes.Rgb48, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    public void TiffEncoder_StripLength<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestStripLength(provider, photometricInterpretation, compression);

    [Theory]
    [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw)]
    public void TiffEncoder_StripLength_WithPalette<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestStripLength(provider, photometricInterpretation, compression, false, 0.01f);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.L8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax)]
    public void TiffEncoder_StripLength_OutOfBounds<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        //// CcittGroup3Fax compressed data length can be larger than the original length.
        Assert.Throws<Xunit.Sdk.TrueException>(() => TestStripLength(provider, photometricInterpretation, compression));

    [Theory]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb)]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.PaletteColor)]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.BlackIsZero)]
    public void TiffEncode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);
        using Image<TPixel> image = provider.GetImage();

        TiffEncoder encoder = new() { PhotometricInterpretation = photometricInterpretation };
        image.DebugSave(provider, encoder);
    }
}
