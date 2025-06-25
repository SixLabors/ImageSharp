// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class PaletteTiffColorTests : PhotometricInterpretationTestBase
{
    public static uint[][] Palette4ColorPalette => GeneratePalette(16);

    public static ushort[] Palette4ColorMap => GenerateColorMap(Palette4ColorPalette);

    private static readonly byte[] Palette4Bytes4X4 =
    [
        0x01, 0x23, 0x4A, 0xD2, 0x12, 0x34, 0xAB, 0xEF
    ];

    private static readonly Rgba32[][] Palette4Result4X4 = GenerateResult(
        Palette4ColorPalette,
        [
            [0x00, 0x01, 0x02, 0x03], [0x04, 0x0A, 0x0D, 0x02], [0x01, 0x02, 0x03, 0x04], [0x0A, 0x0B, 0x0E, 0x0F]
        ]);

    private static readonly byte[] Palette4Bytes3X4 =
    [
        0x01, 0x20,
        0x4A, 0xD0,
        0x12, 0x30,
        0xAB, 0xE0
    ];

    private static readonly Rgba32[][] Palette4Result3X4 = GenerateResult(Palette4ColorPalette, [
        [0x00, 0x01, 0x02], [0x04, 0x0A, 0x0D], [0x01, 0x02, 0x03], [0x0A, 0x0B, 0x0E]
    ]);

    public static IEnumerable<object[]> Palette4Data
    {
        get
        {
            yield return [Palette4Bytes4X4, 4, Palette4ColorMap, 0, 0, 4, 4, Palette4Result4X4];
            yield return [Palette4Bytes4X4, 4, Palette4ColorMap, 0, 0, 4, 4, Offset(Palette4Result4X4, 0, 0, 6, 6)];
            yield return [Palette4Bytes4X4, 4, Palette4ColorMap, 1, 0, 4, 4, Offset(Palette4Result4X4, 1, 0, 6, 6)];
            yield return [Palette4Bytes4X4, 4, Palette4ColorMap, 0, 1, 4, 4, Offset(Palette4Result4X4, 0, 1, 6, 6)];
            yield return [Palette4Bytes4X4, 4, Palette4ColorMap, 1, 1, 4, 4, Offset(Palette4Result4X4, 1, 1, 6, 6)];

            yield return [Palette4Bytes3X4, 4, Palette4ColorMap, 0, 0, 3, 4, Palette4Result3X4];
            yield return [Palette4Bytes3X4, 4, Palette4ColorMap, 0, 0, 3, 4, Offset(Palette4Result3X4, 0, 0, 6, 6)];
            yield return [Palette4Bytes3X4, 4, Palette4ColorMap, 1, 0, 3, 4, Offset(Palette4Result3X4, 1, 0, 6, 6)];
            yield return [Palette4Bytes3X4, 4, Palette4ColorMap, 0, 1, 3, 4, Offset(Palette4Result3X4, 0, 1, 6, 6)];
            yield return [Palette4Bytes3X4, 4, Palette4ColorMap, 1, 1, 3, 4, Offset(Palette4Result3X4, 1, 1, 6, 6)];
        }
    }

    public static uint[][] Palette8ColorPalette => GeneratePalette(256);

    public static ushort[] Palette8ColorMap => GenerateColorMap(Palette8ColorPalette);

    private static readonly byte[] Palette8Bytes4X4 =
    [
        000, 001, 002, 003,
        100, 110, 120, 130,
        000, 255, 128, 255,
        050, 100, 150, 200
    ];

    private static readonly Rgba32[][] Palette8Result4X4 = GenerateResult(Palette8ColorPalette, [
        [000, 001, 002, 003], [100, 110, 120, 130], [000, 255, 128, 255], [050, 100, 150, 200]
    ]);

    public static IEnumerable<object[]> Palette8Data
    {
        get
        {
            yield return [Palette8Bytes4X4, 8, Palette8ColorMap, 0, 0, 4, 4, Palette8Result4X4];
            yield return [Palette8Bytes4X4, 8, Palette8ColorMap, 0, 0, 4, 4, Offset(Palette8Result4X4, 0, 0, 6, 6)];
            yield return [Palette8Bytes4X4, 8, Palette8ColorMap, 1, 0, 4, 4, Offset(Palette8Result4X4, 1, 0, 6, 6)];
            yield return [Palette8Bytes4X4, 8, Palette8ColorMap, 0, 1, 4, 4, Offset(Palette8Result4X4, 0, 1, 6, 6)];
            yield return [Palette8Bytes4X4, 8, Palette8ColorMap, 1, 1, 4, 4, Offset(Palette8Result4X4, 1, 1, 6, 6)];
        }
    }

    [Theory]
    [MemberData(nameof(Palette4Data))]
    [MemberData(nameof(Palette8Data))]
    public void Decode_WritesPixelData(byte[] inputData, ushort bitsPerSample, ushort[] colorMap, int left, int top, int width, int height, Rgba32[][] expectedResult)
        => AssertDecode(expectedResult, pixels =>
            {
                new PaletteTiffColor<Rgba32>(new TiffBitsPerSample(bitsPerSample, 0, 0), colorMap).Decode(inputData, pixels, left, top, width, height);
            });

    private static uint[][] GeneratePalette(int count)
    {
        uint[][] palette = new uint[count][];

        for (uint i = 0; i < count; i++)
        {
            palette[i] = [(i * 2u) % 65536u, (i * 2625u) % 65536u, (i * 29401u) % 65536u];
        }

        return palette;
    }

    private static ushort[] GenerateColorMap(uint[][] colorPalette)
    {
        int colorCount = colorPalette.Length;
        ushort[] colorMap = new ushort[colorCount * 3];

        for (int i = 0; i < colorCount; i++)
        {
            colorMap[(colorCount * 0) + i] = (ushort)colorPalette[i][0];
            colorMap[(colorCount * 1) + i] = (ushort)colorPalette[i][1];
            colorMap[(colorCount * 2) + i] = (ushort)colorPalette[i][2];
        }

        return colorMap;
    }

    private static Rgba32[][] GenerateResult(uint[][] colorPalette, int[][] pixelLookup)
    {
        Rgba32[][] result = new Rgba32[pixelLookup.Length][];

        for (int y = 0; y < pixelLookup.Length; y++)
        {
            result[y] = new Rgba32[pixelLookup[y].Length];

            for (int x = 0; x < pixelLookup[y].Length; x++)
            {
                uint[] sourceColor = colorPalette[pixelLookup[y][x]];
                result[y][x] = new Rgba32(sourceColor[0] / 65535F, sourceColor[1] / 65535F, sourceColor[2] / 65535F);
            }
        }

        return result;
    }
}
