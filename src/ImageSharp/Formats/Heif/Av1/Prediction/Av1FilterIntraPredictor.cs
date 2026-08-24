// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static class Av1FilterIntraPredictor
{
    private const int BufferStride = 33;
    private const int BufferLength = BufferStride * BufferStride;
    private const int TapsPerPixel = 7;
    private const int PixelsPerGroup = 8;
    private const int TapsPerMode = TapsPerPixel * PixelsPerGroup;

    // AV1 7.11.2.3 defines five sets of eight filters over the same seven
    // already-reconstructed neighbors. The omitted eighth libaom tap is zero.
    internal static readonly sbyte[] Taps =
    [

        // DC
        -6, 10, 0, 0, 0, 12, 0,
        -5, 2, 10, 0, 0, 9, 0,
        -3, 1, 1, 10, 0, 7, 0,
        -3, 1, 1, 2, 10, 5, 0,
        -4, 6, 0, 0, 0, 2, 12,
        -3, 2, 6, 0, 0, 2, 9,
        -3, 2, 2, 6, 0, 2, 7,
        -3, 1, 2, 2, 6, 3, 5,

        // Vertical
        -10, 16, 0, 0, 0, 10, 0,
        -6, 0, 16, 0, 0, 6, 0,
        -4, 0, 0, 16, 0, 4, 0,
        -2, 0, 0, 0, 16, 2, 0,
        -10, 16, 0, 0, 0, 0, 10,
        -6, 0, 16, 0, 0, 0, 6,
        -4, 0, 0, 16, 0, 0, 4,
        -2, 0, 0, 0, 16, 0, 2,

        // Horizontal
        -8, 8, 0, 0, 0, 16, 0,
        -8, 0, 8, 0, 0, 16, 0,
        -8, 0, 0, 8, 0, 16, 0,
        -8, 0, 0, 0, 8, 16, 0,
        -4, 4, 0, 0, 0, 0, 16,
        -4, 0, 4, 0, 0, 0, 16,
        -4, 0, 0, 4, 0, 0, 16,
        -4, 0, 0, 0, 4, 0, 16,

        // Directional 157 degrees
        -2, 8, 0, 0, 0, 10, 0,
        -1, 3, 8, 0, 0, 6, 0,
        -1, 2, 3, 8, 0, 4, 0,
        0, 1, 2, 3, 8, 2, 0,
        -1, 4, 0, 0, 0, 3, 10,
        -1, 3, 4, 0, 0, 4, 6,
        -1, 2, 3, 4, 0, 4, 4,
        -1, 2, 2, 3, 4, 3, 3,

        // Paeth
        -12, 14, 0, 0, 0, 14, 0,
        -10, 0, 14, 0, 0, 12, 0,
        -9, 0, 0, 14, 0, 11, 0,
        -8, 0, 0, 0, 14, 10, 0,
        -10, 12, 0, 0, 0, 0, 14,
        -9, 1, 12, 0, 0, 0, 12,
        -8, 0, 0, 12, 0, 1, 11,
        -7, 0, 0, 1, 12, 1, 9,
    ];

    internal static void Predict(
        Span<byte> destination,
        nuint destinationStride,
        Av1TransformSize transformSize,
        Span<byte> above,
        Span<byte> left,
        Av1FilterIntraMode mode)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        DebugGuard.MustBeLessThanOrEqualTo(width, 32, nameof(width));
        DebugGuard.MustBeLessThanOrEqualTo(height, 32, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(destinationStride, (nuint)width, nameof(destinationStride));
        Guard.MustBeSizedAtLeast(destination, (int)destinationStride * height, nameof(destination));
        Guard.MustBeSizedAtLeast(above, width, nameof(above));
        Guard.MustBeSizedAtLeast(left, height, nameof(left));

        Span<byte> buffer = stackalloc byte[BufferLength];
        ref byte bufferRef = ref buffer[0];
        ref byte aboveRef = ref above[0];
        ref byte leftRef = ref left[0];

        // Row zero includes the top-left sample followed by the top neighbors.
        bufferRef = Unsafe.Subtract(ref aboveRef, 1);
        above[..width].CopyTo(buffer[1..]);
        for (int row = 0; row < height; row++)
        {
            Unsafe.Add(ref bufferRef, (row + 1) * BufferStride) = Unsafe.Add(ref leftRef, row);
        }

        int modeOffset = (int)mode * TapsPerMode;
        ref sbyte tapsRef = ref Taps[modeOffset];
        for (int row = 1; row <= height; row += 2)
        {
            for (int column = 1; column <= width; column += 4)
            {
                int sourceOffset = ((row - 1) * BufferStride) + column - 1;
                int p0 = Unsafe.Add(ref bufferRef, sourceOffset);
                int p1 = Unsafe.Add(ref bufferRef, sourceOffset + 1);
                int p2 = Unsafe.Add(ref bufferRef, sourceOffset + 2);
                int p3 = Unsafe.Add(ref bufferRef, sourceOffset + 3);
                int p4 = Unsafe.Add(ref bufferRef, sourceOffset + 4);
                int p5 = Unsafe.Add(ref bufferRef, sourceOffset + BufferStride);
                int p6 = Unsafe.Add(ref bufferRef, sourceOffset + (2 * BufferStride));

                for (int pixel = 0; pixel < PixelsPerGroup; pixel++)
                {
                    int tapOffset = pixel * TapsPerPixel;
                    int prediction =
                        (Unsafe.Add(ref tapsRef, tapOffset) * p0)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 1) * p1)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 2) * p2)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 3) * p3)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 4) * p4)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 5) * p5)
                        + (Unsafe.Add(ref tapsRef, tapOffset + 6) * p6);

                    int rowOffset = pixel >> 2;
                    int columnOffset = pixel & 3;
                    int destinationOffset = ((row + rowOffset) * BufferStride) + column + columnOffset;
                    Unsafe.Add(ref bufferRef, destinationOffset) =
                        (byte)Av1Math.Clamp(Av1Math.RoundPowerOf2(prediction, 4), 0, 255);
                }
            }
        }

        ref byte destinationRef = ref destination[0];
        for (int row = 0; row < height; row++)
        {
            buffer.Slice(((row + 1) * BufferStride) + 1, width).CopyTo(
                MemoryMarshal.CreateSpan(ref destinationRef, width));

            destinationRef = ref Unsafe.Add(ref destinationRef, destinationStride);
        }
    }
}
