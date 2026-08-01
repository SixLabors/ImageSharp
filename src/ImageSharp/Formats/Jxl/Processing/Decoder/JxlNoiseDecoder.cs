// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlNoiseDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BitsToFloatingPoint(ReadOnlySpan<uint> randomBits, Span<float> floats)
    {
        Vector<uint> bits = new(randomBits);
        Vector<float> rand12 = ((bits >> 9) | new Vector<uint>(0x3F800000u)).As<uint, float>();
        rand12.StoreUnsafe(ref MemoryMarshal.GetReference(floats));
    }

    public static void GenerateRandomImage(JxlXorShift rng, Rectangle rectangle, JxlImageF noise)
    {
        const int floatsPerBatch = JxlXorShift.Generators * sizeof(ulong) / sizeof(float);

        int xSize = rectangle.Width;
        int ySize = rectangle.Height;

        Span<ulong> batch64 = stackalloc ulong[JxlXorShift.Generators];
        Span<uint> batch32 = stackalloc uint[JxlXorShift.Generators * 2];

        // stackalloc doesn't zero-initialize, so clear values
        batch64.Clear();
        batch32.Clear();

        int n = Vector<float>.Count;

        for (int y = 0; y < ySize; y++)
        {
            Span<float> row = noise.GetRow(rectangle, y);
            int x = 0;
            for (; x + floatsPerBatch < xSize; x += floatsPerBatch)
            {
                rng.Fill(batch64);
                MemoryMarshal.Cast<uint, ulong>(batch32).CopyTo(batch64);
                for (int i = 0; i < floatsPerBatch; i += n)
                {
                    BitsToFloatingPoint(batch32[i..], row[(x + i)..]);
                }
            }

            rng.Fill(batch64);
            MemoryMarshal.Cast<uint, ulong>(batch32).CopyTo(batch64);

            int batchPos = 0;

            for (; x < xSize; x += n)
            {
                BitsToFloatingPoint(batch32[batchPos..], row[x..]);
                batchPos += n;
            }
        }
    }
}
