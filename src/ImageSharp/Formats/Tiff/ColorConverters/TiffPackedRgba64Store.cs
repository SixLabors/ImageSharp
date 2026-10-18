// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Stores packed RGBA64 groups after byte-order and alpha processing.
/// </summary>
internal readonly struct TiffPackedRgba64Store : ITiffColorStore<ushort, ushort>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 4;

    /// <inheritdoc/>
    public void Store(ref ushort destination, ushort c0, ushort c1, ushort c2, ushort c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1) = c1;
        Unsafe.Add(ref destination, 2) = c2;
        Unsafe.Add(ref destination, 3) = c3;
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector128<ushort> c0, Vector128<ushort> c1, Vector128<ushort> c2, Vector128<ushort> c3)
    {
        c0.StoreUnsafe(ref destination);
        c1.StoreUnsafe(ref destination, 8);
        c2.StoreUnsafe(ref destination, 16);
        c3.StoreUnsafe(ref destination, 24);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector256<ushort> c0, Vector256<ushort> c1, Vector256<ushort> c2, Vector256<ushort> c3)
    {
        c0.StoreUnsafe(ref destination);
        c1.StoreUnsafe(ref destination, 16);
        c2.StoreUnsafe(ref destination, 32);
        c3.StoreUnsafe(ref destination, 48);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector512<ushort> c0, Vector512<ushort> c1, Vector512<ushort> c2, Vector512<ushort> c3)
    {
        c0.StoreUnsafe(ref destination);
        c1.StoreUnsafe(ref destination, 32);
        c2.StoreUnsafe(ref destination, 64);
        c3.StoreUnsafe(ref destination, 96);
    }
}
