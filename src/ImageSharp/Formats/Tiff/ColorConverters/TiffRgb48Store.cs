// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

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
        Unsafe.Add(ref destination, 1U) = c1;
        Unsafe.Add(ref destination, 2U) = c2;
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector128<ushort> c0, Vector128<ushort> c1, Vector128<ushort> c2, Vector128<ushort> c3)
    {
        SimdUtils.Interleave3(c0, c1, c2, ref destination);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector256<ushort> c0, Vector256<ushort> c1, Vector256<ushort> c2, Vector256<ushort> c3)
    {
        // The shared interleave keeps the complete 256-bit group in registers and writes consecutive pixels.
        SimdUtils.Interleave3(c0, c1, c2, ref destination);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector512<ushort> c0, Vector512<ushort> c1, Vector512<ushort> c2, Vector512<ushort> c3)
    {
        // The shared interleave keeps the complete 512-bit group in registers and writes consecutive pixels.
        SimdUtils.Interleave3(c0, c1, c2, ref destination);
    }
}
