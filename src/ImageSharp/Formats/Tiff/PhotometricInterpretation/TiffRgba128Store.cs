// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves exact native uint RGBA lanes into RGBA128 pixels.
/// </summary>
internal readonly struct TiffRgba128Store : ITiffColorStore<uint, uint>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 4;

    /// <inheritdoc/>
    public void Store(ref uint destination, uint c0, uint c1, uint c2, uint c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1) = c1;
        Unsafe.Add(ref destination, 2) = c2;
        Unsafe.Add(ref destination, 3) = c3;
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector128<uint> c0, Vector128<uint> c1, Vector128<uint> c2, Vector128<uint> c3)
    {
        // The float views only transpose raw bits; no uint value is numerically converted.
        SimdUtils.Transpose4(
            c0.AsSingle(),
            c1.AsSingle(),
            c2.AsSingle(),
            c3.AsSingle(),
            out Vector128<float> pixel0,
            out Vector128<float> pixel1,
            out Vector128<float> pixel2,
            out Vector128<float> pixel3);

        pixel0.AsUInt32().StoreUnsafe(ref destination);
        pixel1.AsUInt32().StoreUnsafe(ref destination, 4);
        pixel2.AsUInt32().StoreUnsafe(ref destination, 8);
        pixel3.AsUInt32().StoreUnsafe(ref destination, 12);
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector256<uint> c0, Vector256<uint> c1, Vector256<uint> c2, Vector256<uint> c3)
    {
        SimdUtils.Interleave4(c0.AsSingle(), c1.AsSingle(), c2.AsSingle(), c3.AsSingle(), ref Unsafe.As<uint, float>(ref destination));
    }

    /// <inheritdoc/>
    public void Store(ref uint destination, Vector512<uint> c0, Vector512<uint> c1, Vector512<uint> c2, Vector512<uint> c3)
    {
        SimdUtils.Interleave4(c0.AsSingle(), c1.AsSingle(), c2.AsSingle(), c3.AsSingle(), ref Unsafe.As<uint, float>(ref destination));
    }
}
