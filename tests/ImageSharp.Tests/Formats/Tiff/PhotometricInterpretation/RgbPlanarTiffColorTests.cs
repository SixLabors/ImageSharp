// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation
{
    public class RgbPlanarTiffColorTests : PhotometricInterpretationTestBase
    {
        private static Rgba32 rgb4_000 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 rgb4_444 = new Rgba32(68, 68, 68, 255);
        private static Rgba32 rgb4_888 = new Rgba32(136, 136, 136, 255);
        private static Rgba32 rgb4_CCC = new Rgba32(204, 204, 204, 255);
        private static Rgba32 rgb4_FFF = new Rgba32(255, 255, 255, 255);
        private static Rgba32 rgb4_F00 = new Rgba32(255, 0, 0, 255);
        private static Rgba32 rgb4_0F0 = new Rgba32(0, 255, 0, 255);
        private static Rgba32 rgb4_00F = new Rgba32(0, 0, 255, 255);
        private static Rgba32 rgb4_F0F = new Rgba32(255, 0, 255, 255);
        private static Rgba32 rgb4_400 = new Rgba32(68, 0, 0, 255);
        private static Rgba32 rgb4_800 = new Rgba32(136, 0, 0, 255);
        private static Rgba32 rgb4_C00 = new Rgba32(204, 0, 0, 255);
        private static Rgba32 rgb4_48C = new Rgba32(68, 136, 204, 255);

        private static byte[] rgb4_Bytes4x4_R =
        {
            0x0F, 0x0F,
            0xF0, 0x0F,
            0x48, 0xC4,
            0x04, 0x8C
        };

        private static byte[] rgb4_Bytes4x4_G =
        {
            0x0F, 0x0F,
            0x0F, 0x00,
            0x00, 0x08,
            0x04, 0x8C
        };

        private static byte[] rgb4_Bytes4x4_B =
        {
            0x0F, 0x0F,
            0x00, 0xFF,
            0x00, 0x0C,
            0x04, 0x8C
        };

        private static byte[][] rgb4_Bytes4x4 = { rgb4_Bytes4x4_R, rgb4_Bytes4x4_G, rgb4_Bytes4x4_B };

        private static Rgba32[][] rgb4_Result4x4 =
        {
            new[] { rgb4_000, rgb4_FFF, rgb4_000, rgb4_FFF },
            new[] { rgb4_F00, rgb4_0F0, rgb4_00F, rgb4_F0F },
            new[] { rgb4_400, rgb4_800, rgb4_C00, rgb4_48C },
            new[] { rgb4_000, rgb4_444, rgb4_888, rgb4_CCC }
        };

        private static byte[] rgb4_Bytes3x4_R =
        {
            0x0F, 0x00,
            0xF0, 0x00,
            0x48, 0xC0,
            0x04, 0x80
        };

        private static byte[] rgb4_Bytes3x4_G =
        {
            0x0F, 0x00,
            0x0F, 0x00,
            0x00, 0x00,
            0x04, 0x80
        };

        private static byte[] rgb4_Bytes3x4_B =
        {
            0x0F, 0x00,
            0x00, 0xF0,
            0x00, 0x00,
            0x04, 0x80
        };

        private static byte[][] rgb4_Bytes3x4 = { rgb4_Bytes3x4_R, rgb4_Bytes3x4_G, rgb4_Bytes3x4_B };

        private static Rgba32[][] rgb4_Result3x4 =
        {
            new[] { rgb4_000, rgb4_FFF, rgb4_000 },
            new[] { rgb4_F00, rgb4_0F0, rgb4_00F },
            new[] { rgb4_400, rgb4_800, rgb4_C00 },
            new[] { rgb4_000, rgb4_444, rgb4_888 }
        };

        public static IEnumerable<object[]> Rgb4_Data
        {
            get
            {
                yield return new object[] { rgb4_Bytes4x4, new ushort[] { 4, 4, 4 }, 0, 0, 4, 4, rgb4_Result4x4 };
                yield return new object[] { rgb4_Bytes4x4, new ushort[] { 4, 4, 4 }, 0, 0, 4, 4, Offset(rgb4_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { rgb4_Bytes4x4, new ushort[] { 4, 4, 4 }, 1, 0, 4, 4, Offset(rgb4_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { rgb4_Bytes4x4, new ushort[] { 4, 4, 4 }, 0, 1, 4, 4, Offset(rgb4_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { rgb4_Bytes4x4, new ushort[] { 4, 4, 4 }, 1, 1, 4, 4, Offset(rgb4_Result4x4, 1, 1, 6, 6) };

                yield return new object[] { rgb4_Bytes3x4, new ushort[] { 4, 4, 4 }, 0, 0, 3, 4, rgb4_Result3x4 };
                yield return new object[] { rgb4_Bytes3x4, new ushort[] { 4, 4, 4 }, 0, 0, 3, 4, Offset(rgb4_Result3x4, 0, 0, 6, 6) };
                yield return new object[] { rgb4_Bytes3x4, new ushort[] { 4, 4, 4 }, 1, 0, 3, 4, Offset(rgb4_Result3x4, 1, 0, 6, 6) };
                yield return new object[] { rgb4_Bytes3x4, new ushort[] { 4, 4, 4 }, 0, 1, 3, 4, Offset(rgb4_Result3x4, 0, 1, 6, 6) };
                yield return new object[] { rgb4_Bytes3x4, new ushort[] { 4, 4, 4 }, 1, 1, 3, 4, Offset(rgb4_Result3x4, 1, 1, 6, 6) };
            }
        }

        private static Rgba32 rgb8_000 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 rgb8_444 = new Rgba32(64, 64, 64, 255);
        private static Rgba32 rgb8_888 = new Rgba32(128, 128, 128, 255);
        private static Rgba32 rgb8_CCC = new Rgba32(192, 192, 192, 255);
        private static Rgba32 rgb8_FFF = new Rgba32(255, 255, 255, 255);
        private static Rgba32 rgb8_F00 = new Rgba32(255, 0, 0, 255);
        private static Rgba32 rgb8_0F0 = new Rgba32(0, 255, 0, 255);
        private static Rgba32 rgb8_00F = new Rgba32(0, 0, 255, 255);
        private static Rgba32 rgb8_F0F = new Rgba32(255, 0, 255, 255);
        private static Rgba32 rgb8_400 = new Rgba32(64, 0, 0, 255);
        private static Rgba32 rgb8_800 = new Rgba32(128, 0, 0, 255);
        private static Rgba32 rgb8_C00 = new Rgba32(192, 0, 0, 255);
        private static Rgba32 rgb8_48C = new Rgba32(64, 128, 192, 255);

        private static byte[] rgb8_Bytes4x4_R =
        {
            000, 255, 000, 255,
            255, 000, 000, 255,
            064, 128, 192, 064,
            000, 064, 128, 192
        };

        private static byte[] rgb8_Bytes4x4_G =
        {
            000, 255, 000, 255,
            000, 255, 000, 000,
            000, 000, 000, 128,
            000, 064, 128, 192
        };

        private static byte[] rgb8_Bytes4x4_B =
        {
            000, 255, 000, 255,
            000, 000, 255, 255,
            000, 000, 000, 192,
            000, 064, 128, 192
        };

        private static byte[][] rgb8_Bytes4x4 =
        {
            rgb8_Bytes4x4_R, rgb8_Bytes4x4_G, rgb8_Bytes4x4_B
        };

        private static Rgba32[][] rgb8_Result4x4 =
        {
            new[] { rgb8_000, rgb8_FFF, rgb8_000, rgb8_FFF },
            new[] { rgb8_F00, rgb8_0F0, rgb8_00F, rgb8_F0F },
            new[] { rgb8_400, rgb8_800, rgb8_C00, rgb8_48C },
            new[] { rgb8_000, rgb8_444, rgb8_888, rgb8_CCC }
        };

        public static IEnumerable<object[]> Rgb8_Data
        {
            get
            {
                yield return new object[] { rgb8_Bytes4x4, new ushort[] { 8, 8, 8 }, 0, 0, 4, 4, rgb8_Result4x4 };
                yield return new object[] { rgb8_Bytes4x4, new ushort[] { 8, 8, 8 }, 0, 0, 4, 4, Offset(rgb8_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { rgb8_Bytes4x4, new ushort[] { 8, 8, 8 }, 1, 0, 4, 4, Offset(rgb8_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { rgb8_Bytes4x4, new ushort[] { 8, 8, 8 }, 0, 1, 4, 4, Offset(rgb8_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { rgb8_Bytes4x4, new ushort[] { 8, 8, 8 }, 1, 1, 4, 4, Offset(rgb8_Result4x4, 1, 1, 6, 6) };
            }
        }

        private static Rgba32 rgb484_000 = new Rgba32(0, 0, 0, 255);
        private static Rgba32 rgb484_444 = new Rgba32(68, 64, 68, 255);
        private static Rgba32 rgb484_888 = new Rgba32(136, 128, 136, 255);
        private static Rgba32 rgb484_CCC = new Rgba32(204, 192, 204, 255);
        private static Rgba32 rgb484_FFF = new Rgba32(255, 255, 255, 255);
        private static Rgba32 rgb484_F00 = new Rgba32(255, 0, 0, 255);
        private static Rgba32 rgb484_0F0 = new Rgba32(0, 255, 0, 255);
        private static Rgba32 rgb484_00F = new Rgba32(0, 0, 255, 255);
        private static Rgba32 rgb484_F0F = new Rgba32(255, 0, 255, 255);
        private static Rgba32 rgb484_400 = new Rgba32(68, 0, 0, 255);
        private static Rgba32 rgb484_800 = new Rgba32(136, 0, 0, 255);
        private static Rgba32 rgb484_C00 = new Rgba32(204, 0, 0, 255);
        private static Rgba32 rgb484_48C = new Rgba32(68, 128, 204, 255);

        private static byte[] rgb484_Bytes4x4_R =
        {
            0x0F, 0x0F,
            0xF0, 0x0F,
            0x48, 0xC4,
            0x04, 0x8C
        };

        private static byte[] rgb484_Bytes4x4_G =
        {
            0x00, 0xFF, 0x00, 0xFF,
            0x00, 0xFF, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x80,
            0x00, 0x40, 0x80, 0xC0
        };

        private static byte[] rgb484_Bytes4x4_B =
        {
            0x0F, 0x0F,
            0x00, 0xFF,
            0x00, 0x0C,
            0x04, 0x8C
        };

        private static Rgba32[][] rgb484_Result4x4 =
        {
            new[] { rgb484_000, rgb484_FFF, rgb484_000, rgb484_FFF },
            new[] { rgb484_F00, rgb484_0F0, rgb484_00F, rgb484_F0F },
            new[] { rgb484_400, rgb484_800, rgb484_C00, rgb484_48C },
            new[] { rgb484_000, rgb484_444, rgb484_888, rgb484_CCC }
        };

        private static byte[][] rgb484_Bytes4x4 = { rgb484_Bytes4x4_R, rgb484_Bytes4x4_G, rgb484_Bytes4x4_B };

        public static IEnumerable<object[]> Rgb484_Data
        {
            get
            {
                yield return new object[] { rgb484_Bytes4x4, new ushort[] { 4, 8, 4 }, 0, 0, 4, 4, rgb484_Result4x4 };
                yield return new object[] { rgb484_Bytes4x4, new ushort[] { 4, 8, 4 }, 0, 0, 4, 4, Offset(rgb484_Result4x4, 0, 0, 6, 6) };
                yield return new object[] { rgb484_Bytes4x4, new ushort[] { 4, 8, 4 }, 1, 0, 4, 4, Offset(rgb484_Result4x4, 1, 0, 6, 6) };
                yield return new object[] { rgb484_Bytes4x4, new ushort[] { 4, 8, 4 }, 0, 1, 4, 4, Offset(rgb484_Result4x4, 0, 1, 6, 6) };
                yield return new object[] { rgb484_Bytes4x4, new ushort[] { 4, 8, 4 }, 1, 1, 4, 4, Offset(rgb484_Result4x4, 1, 1, 6, 6) };
            }
        }

        [Theory]
        [MemberData(nameof(Rgb4_Data))]
        [MemberData(nameof(Rgb8_Data))]
        [MemberData(nameof(Rgb484_Data))]
        public void Decode_WritesPixelData(byte[][] inputData, ushort[] bitsPerSample, int left, int top, int width, int height, Rgba32[][] expectedResult)
        {
            AssertDecode(expectedResult, pixels =>
                {
                    var buffers = new IManagedByteBuffer[inputData.Length];
                    for (int i = 0; i < buffers.Length; i++)
                    {
                        buffers[i] = Configuration.Default.MemoryAllocator.AllocateManagedByteBuffer(inputData[i].Length);
                        ((Span<byte>)inputData[i]).CopyTo(buffers[i].GetSpan());
                    }

                    new RgbPlanarTiffColor<Rgba32>(bitsPerSample).Decode(buffers, pixels, left, top, width, height);
                });
        }
    }
}
