// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class BlackIsZeroTiffColorTests : PhotometricInterpretationTestBase
    {
        private static Rgba32 Gray000 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 Gray128 = new Rgba32(128, 128, 128, 255);
        private static Rgba32 Gray255 = new Rgba32(255, 255, 255, 255);
        private static Rgba32 Gray0 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 Gray8 = new Rgba32(136, 136, 136, 255);
        private static Rgba32 GrayF = new Rgba32(255, 255, 255, 255);
        private static Rgba32 Bit0 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 Bit1 = new Rgba32(255, 255, 255, 255);

        private static byte[] Bilevel_Bytes4x4 = new byte[] { 0b01010000,
                                                              0b11110000,
                                                              0b01110000,
                                                              0b10010000 };

        private static Rgba32[][] Bilevel_Result4x4 = new[] { new[] { Bit0, Bit1, Bit0, Bit1 },
                                                             new[] { Bit1, Bit1, Bit1, Bit1 },
                                                             new[] { Bit0, Bit1, Bit1, Bit1 },
                                                             new[] { Bit1, Bit0, Bit0, Bit1 }};

        private static byte[] Bilevel_Bytes12x4 = new byte[] { 0b01010101, 0b01010000,
                                                               0b11111111, 0b11111111,
                                                               0b01101001, 0b10100000,
                                                               0b10010000, 0b01100000};

        private static Rgba32[][] Bilevel_Result12x4 = new[] { new[] { Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1, Bit0, Bit1 },
                                                              new[] { Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1, Bit1 },
                                                              new[] { Bit0, Bit1, Bit1, Bit0, Bit1, Bit0, Bit0, Bit1, Bit1, Bit0, Bit1, Bit0 },
                                                              new[] { Bit1, Bit0, Bit0, Bit1, Bit0, Bit0, Bit0, Bit0, Bit0, Bit1, Bit1, Bit0 }};

        private static byte[] Grayscale4_Bytes4x4 = new byte[] { 0x8F, 0x0F,
                                                                 0xFF, 0xFF,
                                                                 0x08, 0x8F,
                                                                 0xF0, 0xF8 };

        private static Rgba32[][] Grayscale4_Result4x4 = new[] { new[] { Gray8, GrayF, Gray0, GrayF },
                                                                new[] { GrayF, GrayF, GrayF, GrayF },
                                                                new[] { Gray0, Gray8, Gray8, GrayF },
                                                                new[] { GrayF, Gray0, GrayF, Gray8 }};

        private static byte[] Grayscale4_Bytes3x4 = new byte[] { 0x8F, 0x00,
                                                                 0xFF, 0xF0,
                                                                 0x08, 0x80,
                                                                 0xF0, 0xF0 };

        private static Rgba32[][] Grayscale4_Result3x4 = new[] { new[] { Gray8, GrayF, Gray0 },
                                                                new[] { GrayF, GrayF, GrayF },
                                                                new[] { Gray0, Gray8, Gray8 },
                                                                new[] { GrayF, Gray0, GrayF }};

        private static byte[] Grayscale8_Bytes4x4 = new byte[] { 128, 255, 000, 255,
                                                                 255, 255, 255, 255,
                                                                 000, 128, 128, 255,
                                                                 255, 000, 255, 128 };

        private static Rgba32[][] Grayscale8_Result4x4 = new[] { new[] { Gray128, Gray255, Gray000, Gray255 },
                                                                new[] { Gray255, Gray255, Gray255, Gray255 },
                                                                new[] { Gray000, Gray128, Gray128, Gray255 },
                                                                new[] { Gray255, Gray000, Gray255, Gray128 }};

        public static IEnumerable<object[]> Bilevel_Data
        {
            get
            {
                yield return new object[] { Bilevel_Bytes4x4, 1, 0, 0, 4, 4, Bilevel_Result4x4 };
                yield return new object[] { Bilevel_Bytes4x4, 1, 0, 0, 4, 4, Offset(Bilevel_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { Bilevel_Bytes4x4, 1, 1, 0, 4, 4, Offset(Bilevel_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { Bilevel_Bytes4x4, 1, 0, 1, 4, 4, Offset(Bilevel_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { Bilevel_Bytes4x4, 1, 1, 1, 4, 4, Offset(Bilevel_Result4x4, 1, 1, 6, 6) };

                yield return new object[] { Bilevel_Bytes12x4, 1, 0, 0, 12, 4, Bilevel_Result12x4 };
                yield return new object[] { Bilevel_Bytes12x4, 1, 0, 0, 12, 4, Offset(Bilevel_Result12x4, 0, 0, 18, 6) };
                yield return new object[] { Bilevel_Bytes12x4, 1, 1, 0, 12, 4, Offset(Bilevel_Result12x4, 1, 0, 18, 6) };
                yield return new object[] { Bilevel_Bytes12x4, 1, 0, 1, 12, 4, Offset(Bilevel_Result12x4, 0, 1, 18, 6) };
                yield return new object[] { Bilevel_Bytes12x4, 1, 1, 1, 12, 4, Offset(Bilevel_Result12x4, 1, 1, 18, 6) };
            }
        }

        public static IEnumerable<object[]> Grayscale4_Data
        {
            get
            {
                yield return new object[] { Grayscale4_Bytes4x4, 4, 0, 0, 4, 4, Grayscale4_Result4x4 };
                yield return new object[] { Grayscale4_Bytes4x4, 4, 0, 0, 4, 4, Offset(Grayscale4_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { Grayscale4_Bytes4x4, 4, 1, 0, 4, 4, Offset(Grayscale4_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { Grayscale4_Bytes4x4, 4, 0, 1, 4, 4, Offset(Grayscale4_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { Grayscale4_Bytes4x4, 4, 1, 1, 4, 4, Offset(Grayscale4_Result4x4, 1, 1, 6, 6) };

                yield return new object[] { Grayscale4_Bytes3x4, 4, 0, 0, 3, 4, Grayscale4_Result3x4 };
                yield return new object[] { Grayscale4_Bytes3x4, 4, 0, 0, 3, 4, Offset(Grayscale4_Result3x4, 0, 0, 6, 6) };
                yield return new object[] { Grayscale4_Bytes3x4, 4, 1, 0, 3, 4, Offset(Grayscale4_Result3x4, 1, 0, 6, 6) };
                yield return new object[] { Grayscale4_Bytes3x4, 4, 0, 1, 3, 4, Offset(Grayscale4_Result3x4, 0, 1, 6, 6) };
                yield return new object[] { Grayscale4_Bytes3x4, 4, 1, 1, 3, 4, Offset(Grayscale4_Result3x4, 1, 1, 6, 6) };
            }
        }

        public static IEnumerable<object[]> Grayscale8_Data
        {
            get
            {
                yield return new object[] { Grayscale8_Bytes4x4, 8, 0, 0, 4, 4, Grayscale8_Result4x4 };
                yield return new object[] { Grayscale8_Bytes4x4, 8, 0, 0, 4, 4, Offset(Grayscale8_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { Grayscale8_Bytes4x4, 8, 1, 0, 4, 4, Offset(Grayscale8_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { Grayscale8_Bytes4x4, 8, 0, 1, 4, 4, Offset(Grayscale8_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { Grayscale8_Bytes4x4, 8, 1, 1, 4, 4, Offset(Grayscale8_Result4x4, 1, 1, 6, 6) };
            }
        }

        [Theory]
        [MemberData(nameof(Bilevel_Data))]
        [MemberData(nameof(Grayscale4_Data))]
        [MemberData(nameof(Grayscale8_Data))]
        public void Decode_WritesPixelData(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    BlackIsZeroTiffColor.Decode(inputData, new[] { (uint)bitsPerSample }, pixels, left, top, width, height);
                });
        }

        [Theory]
        [MemberData(nameof(Bilevel_Data))]
        public void Decode_WritesPixelData_Bilevel(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    BlackIsZero1TiffColor.Decode(inputData, pixels, left, top, width, height);
                });
        }

        [Theory]
        [MemberData(nameof(Grayscale4_Data))]
        public void Decode_WritesPixelData_4Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    BlackIsZero4TiffColor.Decode(inputData, pixels, left, top, width, height);
                });
        }

        [Theory]
        [MemberData(nameof(Grayscale8_Data))]
        public void Decode_WritesPixelData_8Bit(byte[] inputData, int bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    BlackIsZero8TiffColor.Decode(inputData, pixels, left, top, width, height);
                });
        }
    }
}
