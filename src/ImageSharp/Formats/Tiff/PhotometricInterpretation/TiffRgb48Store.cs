// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves native ushort RGB lanes into RGB48 pixels.
/// </summary>
internal readonly struct TiffRgb48Store : ITiffColorStore<ushort, ushort>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 3;

    /// <inheritdoc/>
    public void Store(ref ushort destination, ushort c0, ushort c1, ushort c2, ushort c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1) = c1;
        Unsafe.Add(ref destination, 2) = c2;
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector128<ushort> c0, Vector128<ushort> c1, Vector128<ushort> c2, Vector128<ushort> c3)
    {
        // Eight R, G and B lanes become 24 words: R0 G0 B0 R1 G1 B1, continuing through R7 G7 B7.
        // Each shuffle places one channel in its output slots. Index 8 produces zero in the other slots.
        Vector128<ushort> first = Vector128.Shuffle(c0, Vector128.Create((ushort)0, 8, 8, 1, 8, 8, 2, 8)) |
            Vector128.Shuffle(c1, Vector128.Create((ushort)8, 0, 8, 8, 1, 8, 8, 2)) |
            Vector128.Shuffle(c2, Vector128.Create((ushort)8, 8, 0, 8, 8, 1, 8, 8));

        Vector128<ushort> second = Vector128.Shuffle(c0, Vector128.Create((ushort)8, 3, 8, 8, 4, 8, 8, 5)) |
            Vector128.Shuffle(c1, Vector128.Create((ushort)8, 8, 3, 8, 8, 4, 8, 8)) |
            Vector128.Shuffle(c2, Vector128.Create((ushort)2, 8, 8, 3, 8, 8, 4, 8));

        Vector128<ushort> third = Vector128.Shuffle(c0, Vector128.Create((ushort)8, 8, 6, 8, 8, 7, 8, 8)) |
            Vector128.Shuffle(c1, Vector128.Create((ushort)5, 8, 8, 6, 8, 8, 7, 8)) |
            Vector128.Shuffle(c2, Vector128.Create((ushort)8, 5, 8, 8, 6, 8, 8, 7));

        first.StoreUnsafe(ref destination);
        second.StoreUnsafe(ref destination, 8);
        third.StoreUnsafe(ref destination, 16);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector256<ushort> c0, Vector256<ushort> c1, Vector256<ushort> c2, Vector256<ushort> c3)
    {
        this.Store(ref destination, c0.GetLower(), c1.GetLower(), c2.GetLower(), default);
        this.Store(ref Unsafe.Add(ref destination, 24), c0.GetUpper(), c1.GetUpper(), c2.GetUpper(), default);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector512<ushort> c0, Vector512<ushort> c1, Vector512<ushort> c2, Vector512<ushort> c3)
    {
        // Four disjoint eight-pixel stores fill exactly ninety-six destination words.
        this.Store(ref destination, c0.GetLower().GetLower(), c1.GetLower().GetLower(), c2.GetLower().GetLower(), default);
        this.Store(ref Unsafe.Add(ref destination, 24), c0.GetLower().GetUpper(), c1.GetLower().GetUpper(), c2.GetLower().GetUpper(), default);
        this.Store(ref Unsafe.Add(ref destination, 48), c0.GetUpper().GetLower(), c1.GetUpper().GetLower(), c2.GetUpper().GetLower(), default);
        this.Store(ref Unsafe.Add(ref destination, 72), c0.GetUpper().GetUpper(), c1.GetUpper().GetUpper(), c2.GetUpper().GetUpper(), default);
    }
}
