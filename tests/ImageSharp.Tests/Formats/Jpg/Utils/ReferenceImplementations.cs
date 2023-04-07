// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

/// <summary>
/// This class contains simplified (inefficient) reference implementations to produce verification data for unit tests
/// Floating point DCT code Ported from https://github.com/norishigefukushima/dct_simd
/// </summary>
internal static partial class ReferenceImplementations
{
    public static void DequantizeBlock(ref Block8x8F block, ref Block8x8F qt, ReadOnlySpan<byte> zigzag)
    {
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            int zig = zigzag[i];
            block[zig] *= qt[i];
        }
    }

    /// <summary>
    /// Transpose 8x8 block stored linearly in a <see cref="Span{T}"/> (inplace)
    /// </summary>
    internal static void Transpose8x8(Span<float> data)
    {
        for (int i = 1; i < 8; i++)
        {
            int i8 = i * 8;
            for (int j = 0; j < i; j++)
            {
                float tmp = data[i8 + j];
                data[i8 + j] = data[(j * 8) + i];
                data[(j * 8) + i] = tmp;
            }
        }
    }

    /// <summary>
    /// Transpose 8x8 block stored linearly in a <see cref="Span{T}"/> (inplace)
    /// </summary>
    internal static void Transpose8x8(Span<short> data)
    {
        for (int i = 1; i < 8; i++)
        {
            int i8 = i * 8;
            for (int j = 0; j < i; j++)
            {
                short tmp = data[i8 + j];
                data[i8 + j] = data[(j * 8) + i];
                data[(j * 8) + i] = tmp;
            }
        }
    }

    /// <summary>
    /// Transpose 8x8 block stored linearly in a  <see cref="Span{T}"/>
    /// </summary>
    internal static void Transpose8x8(Span<float> src, Span<float> dest)
    {
        for (int i = 0; i < 8; i++)
        {
            int i8 = i * 8;
            for (int j = 0; j < 8; j++)
            {
                dest[(j * 8) + i] = src[i8 + j];
            }
        }
    }

    /// <summary>
    /// Copies color values from block to the destination image buffer.
    /// </summary>
    internal static unsafe void CopyColorsTo(ref Block8x8F block, Span<byte> buffer, int stride)
    {
        fixed (Block8x8F* p = &block)
        {
            float* b = (float*)p;

            for (int y = 0; y < 8; y++)
            {
                int y8 = y * 8;
                int yStride = y * stride;

                for (int x = 0; x < 8; x++)
                {
                    float c = b[y8 + x];

                    if (c < -128)
                    {
                        c = 0;
                    }
                    else if (c > 127)
                    {
                        c = 255;
                    }
                    else
                    {
                        c += 128;
                    }

                    buffer[yStride + x] = (byte)c;
                }
            }
        }
    }

    /// <summary>
    /// Reference implementation to test <see cref="Block8x8F.Quantize"/>.
    /// </summary>
    /// <param name="src">The input block.</param>
    /// <param name="dest">The destination block of 16bit integers.</param>
    /// <param name="qt">The quantization table.</param>
    /// <param name="zigzag">Zig-Zag index sequence span.</param>
    public static void Quantize(ref Block8x8F src, ref Block8x8 dest, ref Block8x8F qt, ReadOnlySpan<byte> zigzag)
    {
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            int zig = zigzag[i];
            dest[i] = (short)Math.Round(src[zig] / qt[zig], MidpointRounding.AwayFromZero);
        }
    }
}
