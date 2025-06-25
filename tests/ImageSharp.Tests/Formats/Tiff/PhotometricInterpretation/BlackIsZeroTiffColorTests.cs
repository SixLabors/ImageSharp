// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class BlackIsZeroTiffColorTests : PhotometricInterpretationTestBase
{
    private static readonly Rgba32 Gray000 = new(0, 0, 0, 255);
    private static readonly Rgba32 Gray128 = new(128, 128, 128, 255);
    private static readonly Rgba32 Gray255 = new(255, 255, 255, 255);
    private static readonly Rgba32 Gray0 = new(0, 0, 0, 255);
    private static readonly Rgba32 Gray8 = new(136, 136, 136, 255);
    private static readonly Rgba32 GrayF = new(255, 255, 255, 255);
    private static readonly Rgba32 Bit0 = new(0, 0, 0, 255);
    private static readonly Rgba32 Bit1 = new(255, 255, 255, 255);

    private static readonly byte[] BilevelBytes4X4 =
    [
        0b01010000,
        0b11110000,
        0b01110000,
        0b10010000
    ];

    private static readonly Rgba32[][] BilevelResult4X4 =
    [
        [Bit0, Bit1, Bit0, Bit1],
        [Bit1, Bit1, Bit1, Bit1],
        [Bit0, Bit1, Bit1, Bit1],
        [Bit1, Bit0, Bit0, Bit1]
    ];

    private static readonly byte[] BilevelBytes12X4 =
    [
        0b01010101, 0b01010000,
        0b11111111, 0b11111111,
        0b01101001, 0b10100000,
        0b10010000, 0b01100000
    ];

    private static readonly Rgba32[][] BilevelResult12X4 =
    [
        [Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1],
        [Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1],
        [Bit0, Bit1, Bit1, Bit0, Bit1, Bit0, Bit0, Bit1, Bit1, Bit0, Bit1, Bit0],
        [Bit1, Bit0, Bit0, Bit1, Bit0, Bit0, Bit0, Bit0, Bit0, Bit1, Bit1, Bit0]
    ];

    private static readonly byte[] Grayscale4Bytes4X4 =
    [
        0x8F, 0x0F,
        0xFF, 0xFF,
        0x08, 0x8F,
        0xF0, 0xF8
    ];

    private static readonly Rgba32[][] Grayscale4Result4X4 =
    [
        [Gray8, GrayF, Gray0, GrayF],
        [GrayF, GrayF, GrayF, GrayF],
        [Gray0, Gray8, Gray8, GrayF],
        [GrayF, Gray0, GrayF, Gray8]
    ];

    private static readonly byte[] Grayscale4Bytes3X4 =
    [
        0x8F, 0x00,
        0xFF, 0xF0,
        0x08, 0x80,
        0xF0, 0xF0
    ];

    private static readonly Rgba32[][] Grayscale4Result3X4 =
    [
        [Gray8, GrayF, Gray0],
        [GrayF, GrayF, GrayF],
        [Gray0, Gray8, Gray8],
        [GrayF, Gray0, GrayF]
    ];

    private static readonly byte[] Grayscale8Bytes4X4 =
    [
        128, 255, 000, 255,
        255, 255, 255, 255,
        000, 128, 128, 255,
        255, 000, 255, 128
    ];

    private static readonly Rgba32[][] Grayscale8Result4X4 =
    [
        [Gray128, Gray255, Gray000, Gray255],
        [Gray255, Gray255, Gray255, Gray255],
        [Gray000, Gray128, Gray128, Gray255],
        [Gray255, Gray000, Gray255, Gray128]
    ];

    public static IEnumerable<object[]> BilevelData
    {
        get
        {
            yield return [BilevelBytes4X4, 1, 0, 0, 4, 4, BilevelResult4X4];
            yield return [BilevelBytes4X4, 1, 0, 0, 4, 4, Offset(BilevelResult4X4, 0, 0, 6, 6)];
            yield return [BilevelBytes4X4, 1, 1, 0, 4, 4, Offset(BilevelResult4X4, 1, 0, 6, 6)];
            yield return [BilevelBytes4X4, 1, 0, 1, 4, 4, Offset(BilevelResult4X4, 0, 1, 6, 6)];
            yield return [BilevelBytes4X4, 1, 1, 1, 4, 4, Offset(BilevelResult4X4, 1, 1, 6, 6)];

            yield return [BilevelBytes12X4, 1, 0, 0, 12, 4, BilevelResult12X4];
            yield return [BilevelBytes12X4, 1, 0, 0, 12, 4, Offset(BilevelResult12X4, 0, 0, 18, 6)];
            yield return [BilevelBytes12X4, 1, 1, 0, 12, 4, Offset(BilevelResult12X4, 1, 0, 18, 6)];
            yield return [BilevelBytes12X4, 1, 0, 1, 12, 4, Offset(BilevelResult12X4, 0, 1, 18, 6)];
            yield return [BilevelBytes12X4, 1, 1, 1, 12, 4, Offset(BilevelResult12X4, 1, 1, 18, 6)];
        }
    }

    public static IEnumerable<object[]> Grayscale4_Data
    {
        get
        {
            yield return [Grayscale4Bytes4X4, 4, 0, 0, 4, 4, Grayscale4Result4X4];
            yield return [Grayscale4Bytes4X4, 4, 0, 0, 4, 4, Offset(Grayscale4Result4X4, 0, 0, 6, 6)];
            yield return [Grayscale4Bytes4X4, 4, 1, 0, 4, 4, Offset(Grayscale4Result4X4, 1, 0, 6, 6)];
            yield return [Grayscale4Bytes4X4, 4, 0, 1, 4, 4, Offset(Grayscale4Result4X4, 0, 1, 6, 6)];
            yield return [Grayscale4Bytes4X4, 4, 1, 1, 4, 4, Offset(Grayscale4Result4X4, 1, 1, 6, 6)];

            yield return [Grayscale4Bytes3X4, 4, 0, 0, 3, 4, Grayscale4Result3X4];
            yield return [Grayscale4Bytes3X4, 4, 0, 0, 3, 4, Offset(Grayscale4Result3X4, 0, 0, 6, 6)];
            yield return [Grayscale4Bytes3X4, 4, 1, 0, 3, 4, Offset(Grayscale4Result3X4, 1, 0, 6, 6)];
            yield return [Grayscale4Bytes3X4, 4, 0, 1, 3, 4, Offset(Grayscale4Result3X4, 0, 1, 6, 6)];
            yield return [Grayscale4Bytes3X4, 4, 1, 1, 3, 4, Offset(Grayscale4Result3X4, 1, 1, 6, 6)];
        }
    }

    public static IEnumerable<object[]> Grayscale8_Data
    {
        get
        {
            yield return [Grayscale8Bytes4X4, 8, 0, 0, 4, 4, Grayscale8Result4X4];
            yield return [Grayscale8Bytes4X4, 8, 0, 0, 4, 4, Offset(Grayscale8Result4X4, 0, 0, 6, 6)];
            yield return [Grayscale8Bytes4X4, 8, 1, 0, 4, 4, Offset(Grayscale8Result4X4, 1, 0, 6, 6)];
            yield return [Grayscale8Bytes4X4, 8, 0, 1, 4, 4, Offset(Grayscale8Result4X4, 0, 1, 6, 6)];
            yield return [Grayscale8Bytes4X4, 8, 1, 1, 4, 4, Offset(Grayscale8Result4X4, 1, 1, 6, 6)];
        }
    }

    [Theory]
    [MemberData(nameof(BilevelData))]
    [MemberData(nameof(Grayscale4_Data))]
    [MemberData(nameof(Grayscale8_Data))]
    public void Decode_WritesPixelData(byte[] inputData, ushort bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        => AssertDecode(
            expectedResult,
            pixels => new BlackIsZeroTiffColor<Rgba32>(new TiffBitsPerSample(bitsPerSample, 0, 0)).Decode(inputData, pixels, left, top, width, height));

    [Theory]
    [MemberData(nameof(BilevelData))]
    public void Decode_WritesPixelData_Bilevel(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        => AssertDecode(expectedResult, pixels => new BlackIsZero1TiffColor<Rgba32>().Decode(inputData, pixels, left, top, width, height));

    [Theory]
    [MemberData(nameof(Grayscale4_Data))]
    public void Decode_WritesPixelData_4Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        => AssertDecode(
            expectedResult,
            pixels => new BlackIsZero4TiffColor<Rgba32>().Decode(inputData, pixels, left, top, width, height));

    [Theory]
    [MemberData(nameof(Grayscale8_Data))]
    public void Decode_WritesPixelData_8Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        => AssertDecode(expectedResult, pixels => new BlackIsZero8TiffColor<Rgba32>(Configuration.Default).Decode(inputData, pixels, left, top, width, height));
}
