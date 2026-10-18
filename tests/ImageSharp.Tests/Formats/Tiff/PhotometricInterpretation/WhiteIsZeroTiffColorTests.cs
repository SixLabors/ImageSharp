// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class WhiteIsZeroTiffColorTests : PhotometricInterpretationTestBase
{
    private static readonly Rgba32 Gray000 = new(255, 255, 255, 255);
    private static readonly Rgba32 Gray128 = new(127, 127, 127, 255);
    private static readonly Rgba32 Gray255 = new(0, 0, 0, 255);
    private static readonly Rgba32 Gray0 = new(255, 255, 255, 255);
    private static readonly Rgba32 Gray8 = new(119, 119, 119, 255);
    private static readonly Rgba32 GrayF = new(0, 0, 0, 255);
    private static readonly Rgba32 Bit0 = new(255, 255, 255, 255);
    private static readonly Rgba32 Bit1 = new(0, 0, 0, 255);

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

    public static IEnumerable<object[]> Grayscale4Data
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

    public static IEnumerable<object[]> Grayscale8Data
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

    /// <summary>
    /// Checks byte inversion, row bounds, and unchanged input across SIMD widths and scalar fallback.
    /// </summary>
    [Fact]
    public void Decode8Bit_PreservesSamplesAcrossSimdAndTail() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        Run8BitRows,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks complete byte intensities and row lengths around each SIMD boundary.
    /// </summary>
    private static void Run8BitRows()
    {
        // Short rows exercise each register boundary. The longest row includes every byte value and a partial final block.
        ReadOnlySpan<int> widths = [1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 287];

        foreach (int width in widths)
        {
            const int Height = 2;
            byte[] input = new byte[width * Height];
            Rgba32[][] expectedRows = [new Rgba32[width], new Rgba32[width]];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Consecutive codes cover black, white, and all intermediate levels. Reverse the second row.
                    byte code = (byte)(x % 256);
                    input[(y * width) + x] = y == 0 ? code : (byte)(255 - code);
                    byte intensity = y == 0 ? (byte)(255 - code) : code;
                    expectedRows[y][x] = new Rgba32(intensity, intensity, intensity, 255);
                }
            }

            byte[] unchangedInput = input.ToArray();
            Rgba32[][] expected = Offset(expectedRows, 2, 1, width + 4, Height + 2);
            WhiteIsZero8TiffColor<Rgba32> decoder = new(Configuration.Default);

            // Sentinel pixels around the decoded region detect stores outside either row.
            AssertDecode(expected, pixels => decoder.Decode(input, pixels, 2, 1, width, Height));
            Assert.Equal(unchangedInput, input);
        }
    }

    [Theory]
    [MemberData(nameof(BilevelData))]
    [MemberData(nameof(Grayscale4Data))]
    [MemberData(nameof(Grayscale8Data))]
    public void Decode_WritesPixelData(byte[] inputData, ushort bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
    {
        AssertDecode(expectedResult, pixels =>
            {
                new TiffBitGrayscaleDecoder<Rgba32>(
                    Configuration.Default,
                    bitsPerSample,
                    whiteIsZero: true).Decode(inputData, pixels, left, top, width, height);
            });
    }

    [Theory]
    [MemberData(nameof(BilevelData))]
    public void Decode_WritesPixelData_Bilevel(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
    {
        AssertDecode(expectedResult, pixels =>
            {
                new TiffBitGrayscaleDecoder<Rgba32>(Configuration.Default, 1, whiteIsZero: true).Decode(inputData, pixels, left, top, width, height);
            });
    }

    [Theory]
    [MemberData(nameof(Grayscale4Data))]
    public void Decode_WritesPixelData_4Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
    {
        AssertDecode(expectedResult, pixels =>
            {
                new TiffBitGrayscaleDecoder<Rgba32>(Configuration.Default, 4, whiteIsZero: true).Decode(inputData, pixels, left, top, width, height);
            });
    }

    [Theory]
    [MemberData(nameof(Grayscale8Data))]
    public void Decode_WritesPixelData_8Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
    {
        AssertDecode(expectedResult, pixels =>
            {
                new WhiteIsZero8TiffColor<Rgba32>(Configuration.Default).Decode(inputData, pixels, left, top, width, height);
            });
    }
}
