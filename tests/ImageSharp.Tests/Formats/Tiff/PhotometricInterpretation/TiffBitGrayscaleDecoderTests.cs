// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Checks byte-padded grayscale rows that cross sixteen-pixel SIMD groups and end with a scalar pixel.
/// </summary>
[Trait("Format", "Tiff")]
public class TiffBitGrayscaleDecoderTests : PhotometricInterpretationTestBase
{
    /// <summary>
    /// Verifies both grayscale interpretations for packed rows ending before a full byte.
    /// </summary>
    [Fact]
    public void Decode_WritesPaddedRowsAcrossVectorAndTail() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        RunPackedRows,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks fixed alternating-bit rows at each supported test depth and interpretation.
    /// </summary>
    private static void RunPackedRows()
    {
        // The two code intensities follow directly from 0x55/0xAA at each sample width.
        ReadOnlySpan<(ushort Bits, byte Lower, byte Upper)> cases = [(1, 0, 255), (2, 85, 170), (4, 85, 170), (5, 82, 173), (8, 85, 170), (9, 85, 170)];

        foreach ((ushort bitsPerSample, byte lower, byte upper) in cases)
        {
            DecodePackedRows(bitsPerSample, whiteIsZero: false, lower, upper);
            DecodePackedRows(bitsPerSample, whiteIsZero: true, lower, upper);
        }
    }

    /// <summary>
    /// Checks one code depth and grayscale interpretation against known L8 values.
    /// </summary>
    /// <param name="bitsPerSample">The number of bits in each stored sample.</param>
    /// <param name="whiteIsZero">Whether the greatest code represents black.</param>
    /// <param name="lower">The expected byte intensity from the lower alternating-bit code.</param>
    /// <param name="upper">The expected byte intensity from the higher alternating-bit code.</param>
    private static void DecodePackedRows(ushort bitsPerSample, bool whiteIsZero, byte lower, byte upper)
    {
        // Sixty-five samples cross four sixteen-pixel groups and leave a tail.
        const int width = 65;
        const int height = 2;

        // Rounding up to whole bytes prevents the next row from consuming the previous row's padding bits.
        int rowByteCount = ((width * bitsPerSample) + 7) / 8;
        byte[] packed = new byte[rowByteCount * height];
        Rgba32[][] expectedRows = [new Rgba32[width], new Rgba32[width]];

        // Repeating 01010101 and 10101010 yields 0/1, 1/3 and 2/3, 5/15 and 10/15,
        // 10/31 and 21/31, or 170/511 and 341/511 at the listed sample depths.
        // Their expected L8 bytes are supplied with each case, including float rounding for five and nine bits.

        for (int y = 0; y < height; y++)
        {
            packed.AsSpan(y * rowByteCount, rowByteCount).Fill(y == 0 ? (byte)0x55 : (byte)0xAA);

            for (int x = 0; x < width; x++)
            {
                // Odd sample widths alternate low/high each pixel; even widths repeat one code throughout a row.
                bool high = y == 1;

                if (bitsPerSample % 2 == 1 && x % 2 == 1)
                {
                    high = !high;
                }

                if (whiteIsZero)
                {
                    high = !high;
                }

                byte intensity = high ? upper : lower;
                expectedRows[y][x] = new Rgba32(intensity, intensity, intensity, 255);
            }
        }

        // Offset checks include untouched margins around both decoded rows.
        Rgba32[][] expected = Offset(expectedRows, 2, 1, width + 4, height + 2);
        TiffBitGrayscaleDecoder<Rgba32> decoder = new(Configuration.Default, bitsPerSample, whiteIsZero);
        AssertDecode(expected, pixels => decoder.Decode(packed, pixels, 2, 1, width, height));
    }
}
