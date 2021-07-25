// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation
{
    [Trait("Format", "Tiff")]
    public class RgbTiffColorTests : PhotometricInterpretationTestBase
    {
        private static readonly Rgba32 Rgb4_000 = new Rgba32(0, 0, 0, 255);
        private static readonly Rgba32 Rgb4_444 = new Rgba32(68, 68, 68, 255);
        private static readonly Rgba32 Rgb4_888 = new Rgba32(136, 136, 136, 255);
        private static readonly Rgba32 Rgb4_CCC = new Rgba32(204, 204, 204, 255);
        private static readonly Rgba32 Rgb4_FFF = new Rgba32(255, 255, 255, 255);
        private static readonly Rgba32 Rgb4_F00 = new Rgba32(255, 0, 0, 255);
        private static readonly Rgba32 Rgb4_0F0 = new Rgba32(0, 255, 0, 255);
        private static readonly Rgba32 Rgb4_00F = new Rgba32(0, 0, 255, 255);
        private static readonly Rgba32 Rgb4_F0F = new Rgba32(255, 0, 255, 255);
        private static readonly Rgba32 Rgb4_400 = new Rgba32(68, 0, 0, 255);
        private static readonly Rgba32 Rgb4_800 = new Rgba32(136, 0, 0, 255);
        private static readonly Rgba32 Rgb4_C00 = new Rgba32(204, 0, 0, 255);
        private static readonly Rgba32 Rgb4_48C = new Rgba32(68, 136, 204, 255);

        private static readonly byte[] Rgb4Bytes4X4 =
        {
            0x00, 0x0F, 0xFF, 0x00, 0x0F, 0xFF,
            0xF0, 0x00, 0xF0, 0x00, 0xFF, 0x0F,
            0x40, 0x08, 0x00, 0xC0, 0x04, 0x8C,
            0x00, 0x04, 0x44, 0x88, 0x8C, 0xCC
        };

        private static readonly Rgba32[][] Rgb4Result4X4 =
        {
            new[] { Rgb4_000, Rgb4_FFF, Rgb4_000, Rgb4_FFF },
            new[] { Rgb4_F00, Rgb4_0F0, Rgb4_00F, Rgb4_F0F },
            new[] { Rgb4_400, Rgb4_800, Rgb4_C00, Rgb4_48C },
            new[] { Rgb4_000, Rgb4_444, Rgb4_888, Rgb4_CCC }
        };

        private static readonly byte[] Rgb4Bytes3X4 =
        {
            0x00, 0x0F, 0xFF, 0x00, 0x00,
            0xF0, 0x00, 0xF0, 0x00, 0xF0,
            0x40, 0x08, 0x00, 0xC0, 0x00,
            0x00, 0x04, 0x44, 0x88, 0x80
        };

        private static readonly Rgba32[][] Rgb4Result3X4 =
        {
            new[] { Rgb4_000, Rgb4_FFF, Rgb4_000 },
            new[] { Rgb4_F00, Rgb4_0F0, Rgb4_00F },
            new[] { Rgb4_400, Rgb4_800, Rgb4_C00 },
            new[] { Rgb4_000, Rgb4_444, Rgb4_888 }
        };

        public static IEnumerable<object[]> Rgb4Data
        {
            get
            {
                yield return new object[] { Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 4, 4, Rgb4Result4X4 };
                yield return new object[] { Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 4, 4, Offset(Rgb4Result4X4, 0, 0, 6, 6) };
                yield return new object[] { Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 1, 0, 4, 4, Offset(Rgb4Result4X4, 1, 0, 6, 6) };
                yield return new object[] { Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 0, 1, 4, 4, Offset(Rgb4Result4X4, 0, 1, 6, 6) };
                yield return new object[] { Rgb4Bytes4X4, new TiffBitsPerSample(4, 4, 4), 1, 1, 4, 4, Offset(Rgb4Result4X4, 1, 1, 6, 6) };

                yield return new object[] { Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 3, 4, Rgb4Result3X4 };
                yield return new object[] { Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 0, 3, 4, Offset(Rgb4Result3X4, 0, 0, 6, 6) };
                yield return new object[] { Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 1, 0, 3, 4, Offset(Rgb4Result3X4, 1, 0, 6, 6) };
                yield return new object[] { Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 0, 1, 3, 4, Offset(Rgb4Result3X4, 0, 1, 6, 6) };
                yield return new object[] { Rgb4Bytes3X4, new TiffBitsPerSample(4, 4, 4), 1, 1, 3, 4, Offset(Rgb4Result3X4, 1, 1, 6, 6) };
            }
        }

        private static readonly Rgba32 Rgb8_000 = new Rgba32(0, 0, 0, 255);
        private static readonly Rgba32 Rgb8_444 = new Rgba32(64, 64, 64, 255);
        private static readonly Rgba32 Rgb8_888 = new Rgba32(128, 128, 128, 255);
        private static readonly Rgba32 Rgb8_CCC = new Rgba32(192, 192, 192, 255);
        private static readonly Rgba32 Rgb8_FFF = new Rgba32(255, 255, 255, 255);
        private static readonly Rgba32 Rgb8_F00 = new Rgba32(255, 0, 0, 255);
        private static readonly Rgba32 Rgb8_0F0 = new Rgba32(0, 255, 0, 255);
        private static readonly Rgba32 Rgb8_00F = new Rgba32(0, 0, 255, 255);
        private static readonly Rgba32 Rgb8_F0F = new Rgba32(255, 0, 255, 255);
        private static readonly Rgba32 Rgb8_400 = new Rgba32(64, 0, 0, 255);
        private static readonly Rgba32 Rgb8_800 = new Rgba32(128, 0, 0, 255);
        private static readonly Rgba32 Rgb8_C00 = new Rgba32(192, 0, 0, 255);
        private static readonly Rgba32 Rgb8_48C = new Rgba32(64, 128, 192, 255);

        private static readonly byte[] Rgb8Bytes4X4 =
        {
            000, 000, 000, 255, 255, 255, 000, 000, 000, 255, 255, 255,
            255, 000, 000, 000, 255, 000, 000, 000, 255, 255, 000, 255,
            064, 000, 000, 128, 000, 000, 192, 000, 000, 064, 128, 192,
            000, 000, 000, 064, 064, 064, 128, 128, 128, 192, 192, 192
        };

        private static readonly Rgba32[][] Rgb8Result4X4 =
        {
            new[] { Rgb8_000, Rgb8_FFF, Rgb8_000, Rgb8_FFF },
            new[] { Rgb8_F00, Rgb8_0F0, Rgb8_00F, Rgb8_F0F },
            new[] { Rgb8_400, Rgb8_800, Rgb8_C00, Rgb8_48C },
            new[] { Rgb8_000, Rgb8_444, Rgb8_888, Rgb8_CCC }
        };

        public static IEnumerable<object[]> Rgb8Data
        {
            get
            {
                yield return new object[] { Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 0, 4, 4, Rgb8Result4X4 };
                yield return new object[] { Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 0, 4, 4, Offset(Rgb8Result4X4, 0, 0, 6, 6) };
                yield return new object[] { Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 1, 0, 4, 4, Offset(Rgb8Result4X4, 1, 0, 6, 6) };
                yield return new object[] { Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 0, 1, 4, 4, Offset(Rgb8Result4X4, 0, 1, 6, 6) };
                yield return new object[] { Rgb8Bytes4X4, new TiffBitsPerSample(8, 8, 8), 1, 1, 4, 4, Offset(Rgb8Result4X4, 1, 1, 6, 6) };
            }
        }

        private static readonly Rgba32 Rgb484_000 = new Rgba32(0, 0, 0, 255);
        private static readonly Rgba32 Rgb484_444 = new Rgba32(68, 64, 68, 255);
        private static readonly Rgba32 Rgb484_888 = new Rgba32(136, 128, 136, 255);
        private static readonly Rgba32 Rgb484_CCC = new Rgba32(204, 192, 204, 255);
        private static readonly Rgba32 Rgb484_FFF = new Rgba32(255, 255, 255, 255);
        private static readonly Rgba32 Rgb484_F00 = new Rgba32(255, 0, 0, 255);
        private static readonly Rgba32 Rgb484_0F0 = new Rgba32(0, 255, 0, 255);
        private static readonly Rgba32 Rgb484_00F = new Rgba32(0, 0, 255, 255);
        private static readonly Rgba32 Rgb484_F0F = new Rgba32(255, 0, 255, 255);
        private static readonly Rgba32 Rgb484_400 = new Rgba32(68, 0, 0, 255);
        private static readonly Rgba32 Rgb484_800 = new Rgba32(136, 0, 0, 255);
        private static readonly Rgba32 Rgb484_C00 = new Rgba32(204, 0, 0, 255);
        private static readonly Rgba32 Rgb484_48C = new Rgba32(68, 128, 204, 255);

        private static readonly byte[] Rgb484Bytes4X4 =
        {
            0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF,
            0xF0, 0x00, 0x0F, 0xF0, 0x00, 0x0F, 0xF0, 0x0F,
            0x40, 0x00, 0x80, 0x00, 0xC0, 0x00, 0x48, 0x0C,
            0x00, 0x00, 0x44, 0x04, 0x88, 0x08, 0xCC, 0x0C
        };

        private static readonly Rgba32[][] Rgb484Result4X4 =
        {
            new[] { Rgb484_000, Rgb484_FFF, Rgb484_000, Rgb484_FFF },
            new[] { Rgb484_F00, Rgb484_0F0, Rgb484_00F, Rgb484_F0F },
            new[] { Rgb484_400, Rgb484_800, Rgb484_C00, Rgb484_48C },
            new[] { Rgb484_000, Rgb484_444, Rgb484_888, Rgb484_CCC }
        };

        public static IEnumerable<object[]> Rgb484Data
        {
            get
            {
                yield return new object[] { Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 0, 4, 4, Rgb484Result4X4 };
                yield return new object[] { Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 0, 4, 4, Offset(Rgb484Result4X4, 0, 0, 6, 6) };
                yield return new object[] { Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 1, 0, 4, 4, Offset(Rgb484Result4X4, 1, 0, 6, 6) };
                yield return new object[] { Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 0, 1, 4, 4, Offset(Rgb484Result4X4, 0, 1, 6, 6) };
                yield return new object[] { Rgb484Bytes4X4, new TiffBitsPerSample(4, 8, 4), 1, 1, 4, 4, Offset(Rgb484Result4X4, 1, 1, 6, 6) };
            }
        }

        [Theory]
        [MemberData(nameof(Rgb4Data))]
        [MemberData(nameof(Rgb8Data))]
        [MemberData(nameof(Rgb484Data))]
        public void Decode_WritesPixelData(byte[] inputData, TiffBitsPerSample bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    new RgbTiffColor<Rgba32>(bitsPerSample).Decode(inputData, pixels, left, top, width, height);
                });
        }

        [Theory]
        [MemberData(nameof(Rgb8Data))]
        public void Decode_WritesPixelData_8Bit(byte[] inputData, TiffBitsPerSample bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    new Rgb888TiffColor<Rgba32>().Decode(inputData, pixels, left, top, width, height);
                });
        }
    }
}
