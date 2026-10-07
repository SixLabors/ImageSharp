// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves exact native uint RGB lanes into RGB96 pixels.
/// </summary>
internal readonly struct TiffRgb96Store : ITiffColorStore<uint, uint>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 3;

    /// <inheritdoc/>
    public void Store(ref uint destination, uint c0, uint c1, uint c2, uint c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1) = c1;
        Unsafe.Add(ref destination, 2) = c2;
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector128<uint> c0, Vector128<uint> c1, Vector128<uint> c2, Vector128<uint> c3)
    {
        // Interleave3 uses float views solely for bit-preserving register rearrangement.
        SimdUtils.Interleave3(c0.AsSingle(), c1.AsSingle(), c2.AsSingle(), ref Unsafe.As<uint, float>(ref destination));
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector256<uint> c0, Vector256<uint> c1, Vector256<uint> c2, Vector256<uint> c3)
    {
        SimdUtils.Interleave3(c0.AsSingle(), c1.AsSingle(), c2.AsSingle(), ref Unsafe.As<uint, float>(ref destination));
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector512<uint> c0, Vector512<uint> c1, Vector512<uint> c2, Vector512<uint> c3)
    {
        SimdUtils.Interleave3(c0.AsSingle(), c1.AsSingle(), c2.AsSingle(), ref Unsafe.As<uint, float>(ref destination));
    }
}
