// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves native ushort RGBA lanes into RGBA64 pixels.
/// </summary>
internal readonly struct TiffRgba64Store : ITiffColorStore<ushort, ushort>
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
        // Zip RG and BA ushort pairs, then zip those pairs as 32-bit words.
        // Four exact 128-bit stores cover eight complete RGBA64 pixels.
        Vector128<int> rgLow = Vector128_.UnpackLow(c0.AsInt16(), c1.AsInt16()).AsInt32();
        Vector128<int> rgHigh = Vector128_.UnpackHigh(c0.AsInt16(), c1.AsInt16()).AsInt32();
        Vector128<int> baLow = Vector128_.UnpackLow(c2.AsInt16(), c3.AsInt16()).AsInt32();
        Vector128<int> baHigh = Vector128_.UnpackHigh(c2.AsInt16(), c3.AsInt16()).AsInt32();
        Vector128_.UnpackLow(rgLow, baLow).AsUInt16().StoreUnsafe(ref destination);
        Vector128_.UnpackHigh(rgLow, baLow).AsUInt16().StoreUnsafe(ref destination, 8);
        Vector128_.UnpackLow(rgHigh, baHigh).AsUInt16().StoreUnsafe(ref destination, 16);
        Vector128_.UnpackHigh(rgHigh, baHigh).AsUInt16().StoreUnsafe(ref destination, 24);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector256<ushort> c0, Vector256<ushort> c1, Vector256<ushort> c2, Vector256<ushort> c3)
    {
        this.Store(ref destination, c0.GetLower(), c1.GetLower(), c2.GetLower(), c3.GetLower());
        this.Store(ref Unsafe.Add(ref destination, 32), c0.GetUpper(), c1.GetUpper(), c2.GetUpper(), c3.GetUpper());
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector512<ushort> c0, Vector512<ushort> c1, Vector512<ushort> c2, Vector512<ushort> c3)
    {
        this.Store(ref destination, c0.GetLower().GetLower(), c1.GetLower().GetLower(), c2.GetLower().GetLower(), c3.GetLower().GetLower());
        this.Store(ref Unsafe.Add(ref destination, 32), c0.GetLower().GetUpper(), c1.GetLower().GetUpper(), c2.GetLower().GetUpper(), c3.GetLower().GetUpper());
        this.Store(ref Unsafe.Add(ref destination, 64), c0.GetUpper().GetLower(), c1.GetUpper().GetLower(), c2.GetUpper().GetLower(), c3.GetUpper().GetLower());
        this.Store(ref Unsafe.Add(ref destination, 96), c0.GetUpper().GetUpper(), c1.GetUpper().GetUpper(), c2.GetUpper().GetUpper(), c3.GetUpper().GetUpper());
    }
}
