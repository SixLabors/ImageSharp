// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class PaletteTiffColorTests : PhotometricInterpretationTestBase
    {
        public static uint[][] Palette4_ColorPalette { get => GeneratePalette(16); }

        public static uint[] Palette4_ColorMap { get => GenerateColorMap(Palette4_ColorPalette); }

        private static byte[] Palette4_Bytes4x4 = new byte[] { 0x01, 0x23,
                                                               0x4A, 0xD2,
                                                               0x12, 0x34,
                                                               0xAB, 0xEF };

        private static Rgba32[][] Palette4_Result4x4 = GenerateResult(Palette4_ColorPalette,
                                                        new[] { new[] { 0x00, 0x01, 0x02, 0x03 },
                                                                new[] { 0x04, 0x0A, 0x0D, 0x02 },
                                                                new[] { 0x01, 0x02, 0x03, 0x04 },
                                                                new[] { 0x0A, 0x0B, 0x0E, 0x0F }});

        private static byte[] Palette4_Bytes3x4 = new byte[] { 0x01, 0x20,
                                                               0x4A, 0xD0,
                                                               0x12, 0x30,
                                                               0xAB, 0xE0 };

        private static Rgba32[][] Palette4_Result3x4 = GenerateResult(Palette4_ColorPalette,
                                                        new[] { new[] { 0x00, 0x01, 0x02 },
                                                                new[] { 0x04, 0x0A, 0x0D },
                                                                new[] { 0x01, 0x02, 0x03 },
                                                                new[] { 0x0A, 0x0B, 0x0E }});

        public static IEnumerable<object[]> Palette4_Data
        {
            get
            {
                yield return new object[] { Palette4_Bytes4x4, 4, Palette4_ColorMap, 0, 0, 4, 4, Palette4_Result4x4 };
                yield return new object[] { Palette4_Bytes4x4, 4, Palette4_ColorMap, 0, 0, 4, 4, Offset(Palette4_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { Palette4_Bytes4x4, 4, Palette4_ColorMap, 1, 0, 4, 4, Offset(Palette4_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { Palette4_Bytes4x4, 4, Palette4_ColorMap, 0, 1, 4, 4, Offset(Palette4_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { Palette4_Bytes4x4, 4, Palette4_ColorMap, 1, 1, 4, 4, Offset(Palette4_Result4x4, 1, 1, 6, 6) };

                yield return new object[] { Palette4_Bytes3x4, 4, Palette4_ColorMap, 0, 0, 3, 4, Palette4_Result3x4 };
                yield return new object[] { Palette4_Bytes3x4, 4, Palette4_ColorMap, 0, 0, 3, 4, Offset(Palette4_Result3x4, 0, 0, 6, 6) };
                yield return new object[] { Palette4_Bytes3x4, 4, Palette4_ColorMap, 1, 0, 3, 4, Offset(Palette4_Result3x4, 1, 0, 6, 6) };
                yield return new object[] { Palette4_Bytes3x4, 4, Palette4_ColorMap, 0, 1, 3, 4, Offset(Palette4_Result3x4, 0, 1, 6, 6) };
                yield return new object[] { Palette4_Bytes3x4, 4, Palette4_ColorMap, 1, 1, 3, 4, Offset(Palette4_Result3x4, 1, 1, 6, 6) };

            }
        }

        public static uint[][] Palette8_ColorPalette { get => GeneratePalette(256); }

        public static uint[] Palette8_ColorMap { get => GenerateColorMap(Palette8_ColorPalette); }

        private static byte[] Palette8_Bytes4x4 = new byte[] { 000, 001, 002, 003,
                                                               100, 110, 120, 130,
                                                               000, 255, 128, 255,
                                                               050, 100, 150, 200 };

        private static Rgba32[][] Palette8_Result4x4 = GenerateResult(Palette8_ColorPalette,
                                                        new[] { new[] { 000, 001, 002, 003 },
                                                                new[] { 100, 110, 120, 130 },
                                                                new[] { 000, 255, 128, 255 },
                                                                new[] { 050, 100, 150, 200 }});

        public static IEnumerable<object[]> Palette8_Data
        {
            get
            {
                yield return new object[] { Palette8_Bytes4x4, 8, Palette8_ColorMap, 0, 0, 4, 4, Palette8_Result4x4 };
                yield return new object[] { Palette8_Bytes4x4, 8, Palette8_ColorMap, 0, 0, 4, 4, Offset(Palette8_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { Palette8_Bytes4x4, 8, Palette8_ColorMap, 1, 0, 4, 4, Offset(Palette8_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { Palette8_Bytes4x4, 8, Palette8_ColorMap, 0, 1, 4, 4, Offset(Palette8_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { Palette8_Bytes4x4, 8, Palette8_ColorMap, 1, 1, 4, 4, Offset(Palette8_Result4x4, 1, 1, 6, 6) };
            }
        }

        [Theory]
        [MemberData(nameof(Palette4_Data))]
        [MemberData(nameof(Palette8_Data))]
        public void Decode_WritesPixelData(byte[] inputData, int bitsPerSample, uint[] colorMap, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    PaletteTiffColor.Decode(inputData, new[] { (uint)bitsPerSample }, colorMap, pixels, left, top, width, height);
                });
        }

        private static uint[][] GeneratePalette(int count)
        {
            uint[][] palette = new uint[count][];

            for (uint i = 0; i < count; i++)
            {
                palette[i] = new uint[] { (i * 2u) % 65536u, (i * 2625u) % 65536u, (i * 29401u) % 65536u };
            }

            return palette;
        }

        private static uint[] GenerateColorMap(uint[][] colorPalette)
        {
            int colorCount = colorPalette.Length;
            uint[] colorMap = new uint[colorCount * 3];

            for (int i = 0; i < colorCount; i++)
            {
                colorMap[colorCount * 0 + i] = colorPalette[i][0];
                colorMap[colorCount * 1 + i] = colorPalette[i][1];
                colorMap[colorCount * 2 + i] = colorPalette[i][2];
            }

            return colorMap;
        }

        private static Rgba32[][] GenerateResult(uint[][] colorPalette, int[][] pixelLookup)
        {
            var result = new Rgba32[pixelLookup.Length][];

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
}
