// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffFloatRowTests
{
    /// <summary>
    /// Checks the three-component float writer with hardware intrinsics and its scalar fallback.
    /// </summary>
    [Fact]
    public void FloatRgbWriter_PreservesSamplesAcrossSimdAndTail()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunFloatRgbWriter,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks four-component associated float output at zero and nonzero alpha.
    /// </summary>
    [Fact]
    public void FloatRgbaWriter_PreservesAssociatedSamplesAcrossSimdAndTail()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunFloatRgbaWriter,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks single-component float output in both grayscale interpretations.
    /// </summary>
    /// <param name="whiteIsZero">Whether the stored grayscale samples are inverted.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatSingleComponentWriter_PreservesSamplesAcrossSimdAndTail(bool whiteIsZero)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunFloatSingleComponentWriter,
            whiteIsZero,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks BT.709 grayscale output from a four-component floating-point source.
    /// </summary>
    [Fact]
    public void FloatColorToGrayscaleWriter_UsesBt709AcrossSimdAndTail()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunFloatColorToGrayscaleWriter,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Encodes three-component float samples and checks every decoded component bit.
    /// </summary>
    private static void RunFloatRgbWriter()
    {
        // The writer uses 64-pixel blocks. Width 71 gives a full block, then
        // a four-pixel SIMD group and a three-pixel scalar tail.
        const int Width = 71;

        // The explicit negative-zero and NaN-payload bits detect changes that
        // numeric floating-point equality would not expose.
        float[] samples =
        [
            2.5F,
            -.5F,
            0.1F,
            BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),
            float.PositiveInfinity,
            float.NegativeInfinity,
            BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234)),
            .25F
        ];

        using Image<RgbaVector> input = new(Width, 1);
        for (int x = 0; x < Width; x++)
        {
            input[x, 0] = new RgbaVector(
                samples[x % samples.Length],
                samples[(x + 2) % samples.Length],
                samples[(x + 4) % samples.Length],
                .5F);
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, BitsPerPixel = TiffBitsPerPixel.Bit96, Compression = TiffCompression.None });

        stream.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(stream);
        for (int x = 0; x < Width; x++)
        {
            RgbaVector expected = input[x, 0];
            RgbaVector actual = output[x, 0];
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.R), BitConverter.SingleToInt32Bits(actual.R));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.G), BitConverter.SingleToInt32Bits(actual.G));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.B), BitConverter.SingleToInt32Bits(actual.B));
            Assert.Equal(1F, actual.A);
        }
    }

    /// <summary>
    /// Encodes associated samples without losing stored color at zero alpha.
    /// </summary>
    private static void RunFloatRgbaWriter()
    {
        // The second block includes one full SIMD group and a three-pixel tail.
        const int Width = 71;

        using Image<RgbaVectorP> input = new(Width, 1);
        for (int x = 0; x < Width; x++)
        {
            input[x, 0] = new RgbaVectorP(2.5F + (x * .125F), -.5F, .25F, x % 2 == 0 ? 0F : .5F);
        }

        using MemoryStream stream = new();
        input.Save(stream, new TiffEncoder { SampleFormat = TiffSampleFormat.Float, BitsPerPixel = TiffBitsPerPixel.Bit128, Compression = TiffCompression.None });

        stream.Position = 0;
        using Image<RgbaVectorP> output = Image.Load<RgbaVectorP>(stream);
        for (int x = 0; x < Width; x++)
        {
            RgbaVectorP expected = input[x, 0];
            RgbaVectorP actual = output[x, 0];
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.R), BitConverter.SingleToInt32Bits(actual.R));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.G), BitConverter.SingleToInt32Bits(actual.G));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.B), BitConverter.SingleToInt32Bits(actual.B));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.A), BitConverter.SingleToInt32Bits(actual.A));
        }
    }

    /// <summary>
    /// Encodes one-component float samples and checks the result after TIFF decoding.
    /// </summary>
    /// <param name="serialized">Whether to use the WhiteIsZero interpretation.</param>
    private static void RunFloatSingleComponentWriter(string serialized)
    {
        bool whiteIsZero = FeatureTestRunner.Deserialize<bool>(serialized);

        // Exercise the 64-pixel block boundary, a full SIMD group, and its tail.
        const int Width = 71;
        float[] samples = [2.5F, -.5F, .25F, 1F, 0F, 4F];

        using Image<HalfSingle> input = new(Width, 1);
        for (int x = 0; x < Width; x++)
        {
            input[x, 0] = new HalfSingle(samples[x % samples.Length]);
        }

        using MemoryStream stream = new();
        input.Save(
            stream,
            new TiffEncoder
            {
                SampleFormat = TiffSampleFormat.Float,
                BitsPerPixel = TiffBitsPerPixel.Bit32,
                PhotometricInterpretation = whiteIsZero ? TiffPhotometricInterpretation.WhiteIsZero : TiffPhotometricInterpretation.BlackIsZero,
                Compression = TiffCompression.None
            });

        stream.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(stream);
        for (int x = 0; x < Width; x++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(input[x, 0].ToSingle()), BitConverter.SingleToInt32Bits(output[x, 0].R));
        }
    }

    /// <summary>
    /// Encodes color as grayscale and checks the stored BT.709 intensity through decoding.
    /// </summary>
    private static void RunFloatColorToGrayscaleWriter()
    {
        // The seven pixels after the first block exercise both SIMD and scalar packing.
        const int Width = 71;
        using Image<RgbaVector> input = new(Width, 1);

        // One nonzero component per pixel makes the expected luminance independent
        // of floating-point addition order while exposing component-order mistakes.
        for (int x = 0; x < Width; x++)
        {
            input[x, 0] = (x % 3) switch
            {
                0 => new RgbaVector(2F, 0F, 0F),
                1 => new RgbaVector(0F, -2F, 0F),
                _ => new RgbaVector(0F, 0F, 4F)
            };
        }

        using MemoryStream stream = new();
        input.Save(
            stream,
            new TiffEncoder
            {
                SampleFormat = TiffSampleFormat.Float,
                BitsPerPixel = TiffBitsPerPixel.Bit32,
                PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero,
                Compression = TiffCompression.None
            });

        stream.Position = 0;
        using Image<RgbaVector> output = Image.Load<RgbaVector>(stream);
        for (int x = 0; x < Width; x++)
        {
            float expected = (x % 3) switch
            {
                0 => 2F * ColorNumerics.Bt709.X,
                1 => -2F * ColorNumerics.Bt709.Y,
                _ => 4F * ColorNumerics.Bt709.Z
            };

            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(output[x, 0].R));
        }
    }

    [Theory]
    [MemberData(nameof(SimdUtilsTests.ArbitraryArraySizes), MemberType = typeof(SimdUtilsTests))]
    public void FloatDecoderRows_PreserveComponentBitsAcrossSimdAndTail(int count)
    {
        static void RunTest(string serialized) => AssertFloatDecoderRows(FeatureTestRunner.Deserialize<int>(serialized));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks the TIFF row decoders through their Decode contract at SIMD widths and tails.
    /// </summary>
    /// <param name="count">The number of pixels in the row.</param>
    private static void AssertFloatDecoderRows(int count)
    {
        // Payload bits and signed zero reveal conversions that numeric equality misses.
        float[] values =
        [
            BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234)),
            float.PositiveInfinity,
            float.NegativeInfinity,
            BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),
            -2.5F,
            0.1F,
            1F,
            65504F
        ];

        float[] triplets = new float[count * 3];
        float[] quads = new float[count * 4];
        float[] singles = new float[count];
        for (int i = 0; i < count; i++)
        {
            for (int component = 0; component < 4; component++)
            {
                float sample = values[((i * 4) + component) % values.Length];
                quads[(i * 4) + component] = sample;
                if (component < 3)
                {
                    triplets[(i * 3) + component] = sample;
                }
            }

            singles[i] = values[i % values.Length];
        }

        Configuration configuration = Configuration.Default;
        using Buffer2D<RgbaVector> pixels = configuration.MemoryAllocator.Allocate2D<RgbaVector>(Math.Max(count, 1), 1);

        for (int byteOrder = 0; byteOrder < 2; byteOrder++)
        {
            bool reverseEndianness = byteOrder != 0;
            bool isBigEndian = reverseEndianness == BitConverter.IsLittleEndian;
            byte[] packedTriplets = MemoryMarshal.AsBytes(triplets.AsSpan()).ToArray();
            byte[] packedQuads = MemoryMarshal.AsBytes(quads.AsSpan()).ToArray();
            byte[] packedSingles = MemoryMarshal.AsBytes(singles.AsSpan()).ToArray();
            if (reverseEndianness)
            {
                ReverseSamples(packedTriplets);
                ReverseSamples(packedQuads);
                ReverseSamples(packedSingles);
            }

            RgbFloat323232TiffColor<RgbaVector> rgb = new(configuration, isBigEndian);
            rgb.Decode(packedTriplets, pixels, 0, 0, count, 1);
            for (int i = 0; i < count; i++)
            {
                RgbaVector actual = pixels.DangerousGetRowSpan(0)[i];
                Assert.Equal(BitConverter.SingleToInt32Bits(triplets[i * 3]), BitConverter.SingleToInt32Bits(actual.R));
                Assert.Equal(BitConverter.SingleToInt32Bits(triplets[(i * 3) + 1]), BitConverter.SingleToInt32Bits(actual.G));
                Assert.Equal(BitConverter.SingleToInt32Bits(triplets[(i * 3) + 2]), BitConverter.SingleToInt32Bits(actual.B));
                Assert.Equal(1F, actual.A);
            }

            RgbaFloat32323232TiffColor<RgbaVector> rgba = new(configuration, isBigEndian, TiffExtraSampleType.UnassociatedAlphaData);
            rgba.Decode(packedQuads, pixels, 0, 0, count, 1);
            for (int i = 0; i < count; i++)
            {
                RgbaVector actual = pixels.DangerousGetRowSpan(0)[i];
                Assert.Equal(BitConverter.SingleToInt32Bits(quads[i * 4]), BitConverter.SingleToInt32Bits(actual.R));
                Assert.Equal(BitConverter.SingleToInt32Bits(quads[(i * 4) + 1]), BitConverter.SingleToInt32Bits(actual.G));
                Assert.Equal(BitConverter.SingleToInt32Bits(quads[(i * 4) + 2]), BitConverter.SingleToInt32Bits(actual.B));
                Assert.Equal(BitConverter.SingleToInt32Bits(quads[(i * 4) + 3]), BitConverter.SingleToInt32Bits(actual.A));
            }

            BlackIsZero32FloatTiffColor<RgbaVector> grayscale = new(configuration, isBigEndian);
            grayscale.Decode(packedSingles, pixels, 0, 0, count, 1);
            for (int i = 0; i < count; i++)
            {
                RgbaVector actual = pixels.DangerousGetRowSpan(0)[i];
                int expected = BitConverter.SingleToInt32Bits(singles[i]);
                Assert.Equal(expected, BitConverter.SingleToInt32Bits(actual.R));
                Assert.Equal(expected, BitConverter.SingleToInt32Bits(actual.G));
                Assert.Equal(expected, BitConverter.SingleToInt32Bits(actual.B));
                Assert.Equal(1F, actual.A);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SimdUtilsTests.ArbitraryArraySizes), MemberType = typeof(SimdUtilsTests))]
    public void WhiteIsZeroFloatRows_InvertAcrossSimdAndTail(int count)
    {
        static void RunTest(string serialized) => AssertWhiteIsZeroFloatRows(FeatureTestRunner.Deserialize<int>(serialized));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks WhiteIsZero using finite samples, including values outside zero to one.
    /// </summary>
    /// <param name="count">The number of pixels in the row.</param>
    private static void AssertWhiteIsZeroFloatRows(int count)
    {
        float[] values = [0F, 0.25F, 0.5F, 1F, 2F];
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = values[i % values.Length];
        }

        Configuration configuration = Configuration.Default;
        using Buffer2D<RgbaVector> pixels = configuration.MemoryAllocator.Allocate2D<RgbaVector>(Math.Max(count, 1), 1);
        WhiteIsZero32FloatTiffColor<RgbaVector> decoder = new(configuration, isBigEndian: !BitConverter.IsLittleEndian);
        decoder.Decode(MemoryMarshal.AsBytes(samples.AsSpan()), pixels, 0, 0, count, 1);

        for (int i = 0; i < count; i++)
        {
            RgbaVector actual = pixels.DangerousGetRowSpan(0)[i];
            Assert.Equal(1F - samples[i], actual.R);
            Assert.Equal(actual.R, actual.G);
            Assert.Equal(actual.R, actual.B);
            Assert.Equal(1F, actual.A);
        }
    }

    /// <summary>
    /// Reverses each stored float sample without changing sample order.
    /// </summary>
    /// <param name="samples">The packed sample bytes.</param>
    private static void ReverseSamples(Span<byte> samples)
    {
        for (int i = 0; i < samples.Length; i += sizeof(float))
        {
            samples.Slice(i, sizeof(float)).Reverse();
        }
    }
}
