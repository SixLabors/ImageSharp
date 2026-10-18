// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class RgbPlanarTiffColorTests : PhotometricInterpretationTestBase
{
    private static readonly Rgba32 Rgb4_000 = new(0, 0, 0, 255);
    private static readonly Rgba32 Rgb4_444 = new(68, 68, 68, 255);
    private static readonly Rgba32 Rgb4_888 = new(136, 136, 136, 255);
    private static readonly Rgba32 Rgb4_CCC = new(204, 204, 204, 255);
    private static readonly Rgba32 Rgb4_FFF = new(255, 255, 255, 255);
    private static readonly Rgba32 Rgb4_F00 = new(255, 0, 0, 255);
    private static readonly Rgba32 Rgb4_0F0 = new(0, 255, 0, 255);
    private static readonly Rgba32 Rgb4_00F = new(0, 0, 255, 255);
    private static readonly Rgba32 Rgb4_F0F = new(255, 0, 255, 255);
    private static readonly Rgba32 Rgb4_400 = new(68, 0, 0, 255);
    private static readonly Rgba32 Rgb4_800 = new(136, 0, 0, 255);
    private static readonly Rgba32 Rgb4_C00 = new(204, 0, 0, 255);
    private static readonly Rgba32 Rgb4_48C = new(68, 136, 204, 255);

    private static readonly byte[] Rgb4Bytes4X4R =
    [
        0x0F, 0x0F,
        0xF0, 0x0F,
        0x48, 0xC4,
        0x04, 0x8C
    ];

    private static readonly byte[] Rgb4Bytes4X4G =
    [
        0x0F, 0x0F,
        0x0F, 0x00,
        0x00, 0x08,
        0x04, 0x8C
    ];

    private static readonly byte[] Rgb4Bytes4X4B =
    [
        0x0F, 0x0F,
        0x00, 0xFF,
        0x00, 0x0C,
        0x04, 0x8C
    ];

    private static readonly byte[][] Rgb4Bytes4X4 = [Rgb4Bytes4X4R, Rgb4Bytes4X4G, Rgb4Bytes4X4B];

    private static readonly Rgba32[][] Rgb4Result4X4 =
    [
        [Rgb4_000, Rgb4_FFF, Rgb4_000, Rgb4_FFF],
        [Rgb4_F00, Rgb4_0F0, Rgb4_00F, Rgb4_F0F],
        [Rgb4_400, Rgb4_800, Rgb4_C00, Rgb4_48C],
        [Rgb4_000, Rgb4_444, Rgb4_888, Rgb4_CCC]
    ];

    private static readonly byte[] Rgb4Bytes3X4R =
    [
        0x0F, 0x00,
        0xF0, 0x00,
        0x48, 0xC0,
        0x04, 0x80
    ];

    private static readonly byte[] Rgb4Bytes3X4G =
    [
        0x0F, 0x00,
        0x0F, 0x00,
        0x00, 0x00,
        0x04, 0x80
    ];

    private static readonly byte[] Rgb4Bytes3X4B =
    [
        0x0F, 0x00,
        0x00, 0xF0,
        0x00, 0x00,
        0x04, 0x80
    ];

    private static readonly byte[][] Rgb4Bytes3X4 = [Rgb4Bytes3X4R, Rgb4Bytes3X4G, Rgb4Bytes3X4B];

    private static readonly Rgba32[][] Rgb4Result3X4 =
    [
        [Rgb4_000, Rgb4_FFF, Rgb4_000],
        [Rgb4_F00, Rgb4_0F0, Rgb4_00F],
        [Rgb4_400, Rgb4_800, Rgb4_C00],
        [Rgb4_000, Rgb4_444, Rgb4_888]
    ];

    public static IEnumerable<object[]> Rgb4Data
    {
        get
        {
            yield return [Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 4, 4, Rgb4Result4X4];
            yield return [Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 4, 4, Offset(Rgb4Result4X4, 0, 0, 6, 6)];
            yield return [Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 1, 0, 4, 4, Offset(Rgb4Result4X4, 1, 0, 6, 6)];
            yield return [Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 1, 4, 4, Offset(Rgb4Result4X4, 0, 1, 6, 6)];
            yield return [Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 1, 1, 4, 4, Offset(Rgb4Result4X4, 1, 1, 6, 6)];

            yield return [Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 3, 4, Rgb4Result3X4];
            yield return [Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 3, 4, Offset(Rgb4Result3X4, 0, 0, 6, 6)];
            yield return [Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 1, 0, 3, 4, Offset(Rgb4Result3X4, 1, 0, 6, 6)];
            yield return [Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 1, 3, 4, Offset(Rgb4Result3X4, 0, 1, 6, 6)];
            yield return [Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 1, 1, 3, 4, Offset(Rgb4Result3X4, 1, 1, 6, 6)];
        }
    }

    private static readonly Rgba32 Rgb8_000 = new(0, 0, 0, 255);
    private static readonly Rgba32 Rgb8_444 = new(64, 64, 64, 255);
    private static readonly Rgba32 Rgb8_888 = new(128, 128, 128, 255);
    private static readonly Rgba32 Rgb8_CCC = new(192, 192, 192, 255);
    private static readonly Rgba32 Rgb8_FFF = new(255, 255, 255, 255);
    private static readonly Rgba32 Rgb8_F00 = new(255, 0, 0, 255);
    private static readonly Rgba32 Rgb8_0F0 = new(0, 255, 0, 255);
    private static readonly Rgba32 Rgb8_00F = new(0, 0, 255, 255);
    private static readonly Rgba32 Rgb8_F0F = new(255, 0, 255, 255);
    private static readonly Rgba32 Rgb8_400 = new(64, 0, 0, 255);
    private static readonly Rgba32 Rgb8_800 = new(128, 0, 0, 255);
    private static readonly Rgba32 Rgb8_C00 = new(192, 0, 0, 255);
    private static readonly Rgba32 Rgb8_48C = new(64, 128, 192, 255);

    private static readonly byte[] Rgb8Bytes4X4R =
    [
        000, 255, 000, 255,
        255, 000, 000, 255,
        064, 128, 192, 064,
        000, 064, 128, 192
    ];

    private static readonly byte[] Rgb8Bytes4X4G =
    [
        000, 255, 000, 255,
        000, 255, 000, 000,
        000, 000, 000, 128,
        000, 064, 128, 192
    ];

    private static readonly byte[] Rgb8Bytes4X4B =
    [
        000, 255, 000, 255,
        000, 000, 255, 255,
        000, 000, 000, 192,
        000, 064, 128, 192
    ];

    private static readonly byte[][] Rgb8Bytes4X4 =
    [
        Rgb8Bytes4X4R, Rgb8Bytes4X4G, Rgb8Bytes4X4B
    ];

    private static readonly Rgba32[][] Rgb8Result4X4 =
    [
        [Rgb8_000, Rgb8_FFF, Rgb8_000, Rgb8_FFF],
        [Rgb8_F00, Rgb8_0F0, Rgb8_00F, Rgb8_F0F],
        [Rgb8_400, Rgb8_800, Rgb8_C00, Rgb8_48C],
        [Rgb8_000, Rgb8_444, Rgb8_888, Rgb8_CCC]
    ];

    public static IEnumerable<object[]> Rgb8Data
    {
        get
        {
            yield return [Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 0, 4, 4, Rgb8Result4X4];
            yield return [Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 0, 4, 4, Offset(Rgb8Result4X4, 0, 0, 6, 6)];
            yield return [Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 1, 0, 4, 4, Offset(Rgb8Result4X4, 1, 0, 6, 6)];
            yield return [Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 1, 4, 4, Offset(Rgb8Result4X4, 0, 1, 6, 6)];
            yield return [Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 1, 1, 4, 4, Offset(Rgb8Result4X4, 1, 1, 6, 6)];
        }
    }

    private static readonly Rgba32 Rgb484_000 = new(0, 0, 0, 255);
    private static readonly Rgba32 Rgb484_444 = new(68, 64, 68, 255);
    private static readonly Rgba32 Rgb484_888 = new(136, 128, 136, 255);
    private static readonly Rgba32 Rgb484_CCC = new(204, 192, 204, 255);
    private static readonly Rgba32 Rgb484_FFF = new(255, 255, 255, 255);
    private static readonly Rgba32 Rgb484_F00 = new(255, 0, 0, 255);
    private static readonly Rgba32 Rgb484_0F0 = new(0, 255, 0, 255);
    private static readonly Rgba32 Rgb484_00F = new(0, 0, 255, 255);
    private static readonly Rgba32 Rgb484_F0F = new(255, 0, 255, 255);
    private static readonly Rgba32 Rgb484_400 = new(68, 0, 0, 255);
    private static readonly Rgba32 Rgb484_800 = new(136, 0, 0, 255);
    private static readonly Rgba32 Rgb484_C00 = new(204, 0, 0, 255);
    private static readonly Rgba32 Rgb484_48C = new(68, 128, 204, 255);

    private static readonly byte[] Rgb484Bytes4X4R =
    [
        0x0F, 0x0F,
        0xF0, 0x0F,
        0x48, 0xC4,
        0x04, 0x8C
    ];

    private static readonly byte[] Rgb484Bytes4X4G =
    [
        0x00, 0xFF, 0x00, 0xFF,
        0x00, 0xFF, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x80,
        0x00, 0x40, 0x80, 0xC0
    ];

    private static readonly byte[] Rgb484Bytes4X4B =
    [
        0x0F, 0x0F,
        0x00, 0xFF,
        0x00, 0x0C,
        0x04, 0x8C
    ];

    private static readonly Rgba32[][] Rgb484Result4X4 =
    [
        [Rgb484_000, Rgb484_FFF, Rgb484_000, Rgb484_FFF],
        [Rgb484_F00, Rgb484_0F0, Rgb484_00F, Rgb484_F0F],
        [Rgb484_400, Rgb484_800, Rgb484_C00, Rgb484_48C],
        [Rgb484_000, Rgb484_444, Rgb484_888, Rgb484_CCC]
    ];

    private static readonly byte[][] Rgb484Bytes4X4 = [Rgb484Bytes4X4R, Rgb484Bytes4X4G, Rgb484Bytes4X4B];

    public static IEnumerable<object[]> Rgb484_Data
    {
        get
        {
            yield return [Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 0, 4, 4, Rgb484Result4X4];
            yield return [Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 0, 4, 4, Offset(Rgb484Result4X4, 0, 0, 6, 6)
            ];
            yield return [Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 1, 0, 4, 4, Offset(Rgb484Result4X4, 1, 0, 6, 6)
            ];
            yield return [Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 1, 4, 4, Offset(Rgb484Result4X4, 0, 1, 6, 6)
            ];
            yield return [Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 1, 1, 4, 4, Offset(Rgb484Result4X4, 1, 1, 6, 6)
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Rgb4Data))]
    [MemberData(nameof(Rgb8Data))]
    [MemberData(nameof(Rgb484_Data))]
    public void Decode_WritesPixelData(
        byte[][] inputData,
        TiffBitsPerSample bitsPerSample,
        int left,
        int top,
        int width,
        int height,
        Rgba32[][] expectedResult)
        => AssertDecode(
            expectedResult,
            pixels =>
            {
                IMemoryOwner<byte>[] buffers = new IMemoryOwner<byte>[inputData.Length];
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i] = Configuration.Default.MemoryAllocator.Allocate<byte>(inputData[i].Length);
                    ((Span<byte>)inputData[i]).CopyTo(buffers[i].GetSpan());
                }

                new RgbPlanarTiffColor<Rgba32>(Configuration.Default, bitsPerSample).Decode(buffers, pixels, left, top, width, height);

                foreach (IMemoryOwner<byte> buffer in buffers)
                {
                    buffer.Dispose();
                }
            });

    /// <summary>
    /// Checks byte-plane RGB output retains fractional scaled components in RgbaVector.
    /// </summary>
    /// <param name="length">The row width, including scalar and vector-tail cases.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(65)]
    public void Decode_Aligned8Bit_PreservesRgbaVectorPrecision(int length) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertRgbaVectorPrecision,
        length,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks every decoded RGB component against the stored byte planes.
    /// </summary>
    /// <param name="serialized">The serialized row width.</param>
    private static void AssertRgbaVectorPrecision(string serialized)
    {
        int length = FeatureTestRunner.Deserialize<int>(serialized);
        using IMemoryOwner<byte> red = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> green = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> blue = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        IMemoryOwner<byte>[] planes = [red, green, blue];
        using Image<RgbaVector> image = new(length, 1);
        Span<byte> redSamples = red.GetSpan();
        Span<byte> greenSamples = green.GetSpan();
        Span<byte> blueSamples = blue.GetSpan();

        // Seventeen unequal pixels shift across the decoder's 32-pixel chunks and reveal plane or lane swaps.
        ReadOnlySpan<Rgb24> samples =
        [
            new(0, 255, 1), new(17, 63, 201), new(255, 0, 128), new(255, 255, 255),
            new(3, 31, 157), new(41, 5, 233), new(91, 177, 13), new(222, 19, 109),
            new(72, 213, 44), new(151, 62, 7), new(11, 99, 201), new(192, 8, 73),
            new(64, 144, 224), new(29, 209, 117), new(187, 54, 96), new(237, 83, 16),
            new(103, 147, 231)
        ];

        for (int i = 0; i < length; i++)
        {
            Rgb24 sample = samples[i % samples.Length];
            redSamples[i] = sample.R;
            greenSamples[i] = sample.G;
            blueSamples[i] = sample.B;
        }

        new RgbPlanarTiffColor<RgbaVector>(Configuration.Default, new TiffBitsPerSample(8, 8, 8)).Decode(
            planes,
            image.Frames.RootFrame.PixelBuffer,
            0,
            0,
            length,
            1);

        // Widths 1/17/33/64/65 cover scalar output, tails, and consecutive thirty-two-pixel decoder blocks.
        for (int i = 0; i < length; i++)
        {
            Vector4 expected = new(redSamples[i] / 255F, greenSamples[i] / 255F, blueSamples[i] / 255F, 1F);
            Assert.Equal(expected, image[i, 0].ToScaledVector4());
        }
    }
}
