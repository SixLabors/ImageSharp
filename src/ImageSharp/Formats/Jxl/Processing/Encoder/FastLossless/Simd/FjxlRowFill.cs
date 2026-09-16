// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

internal static class FjxlRowFill
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillRowG8<T>(Span<byte> rgba, int oxs, Span<T> luma)
        where T : unmanaged, INumber<T>
    {
        int x = 0;

        for (; x + FjxlSimdVec16.Lanes <= oxs; x += FjxlSimdVec16.Lanes)
        {
            Vector<byte> rgb = FjxlSimdVec16.LoadG8(rgba[x..]);
            StorePixels(rgb, luma[x..]);
        }
    }
}
