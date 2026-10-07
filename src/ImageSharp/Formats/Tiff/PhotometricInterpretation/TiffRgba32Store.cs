// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Interleaves four byte planes into complete RGBA32 pixels.
/// </summary>
internal readonly struct TiffRgba32Store : ITiffColorStore<byte, byte>
{
    /// <inheritdoc/>
    public static int WordsPerPixel => 4;

    /// <inheritdoc/>
    public void Store(ref byte destination, byte c0, byte c1, byte c2, byte c3)
    {
        destination = c0;
        Unsafe.Add(ref destination, 1) = c1;
        Unsafe.Add(ref destination, 2) = c2;
        Unsafe.Add(ref destination, 3) = c3;
    }

    /// <inheritdoc/>
    public void Store(ref byte destination, Vector128<byte> c0, Vector128<byte> c1, Vector128<byte> c2, Vector128<byte> c3)
    {
        // Byte zips form RG/BA pairs; ushort zips form four RGBA pixels per store.
        Vector128<short> rgLow = Vector128_.UnpackLow(c0, c1).AsInt16();
        Vector128<short> rgHigh = Vector128_.UnpackHigh(c0, c1).AsInt16();
        Vector128<short> baLow = Vector128_.UnpackLow(c2, c3).AsInt16();
        Vector128<short> baHigh = Vector128_.UnpackHigh(c2, c3).AsInt16();
        Vector128_.UnpackLow(rgLow, baLow).AsByte().StoreUnsafe(ref destination);
        Vector128_.UnpackHigh(rgLow, baLow).AsByte().StoreUnsafe(ref destination, 16);
        Vector128_.UnpackLow(rgHigh, baHigh).AsByte().StoreUnsafe(ref destination, 32);
        Vector128_.UnpackHigh(rgHigh, baHigh).AsByte().StoreUnsafe(ref destination, 48);
    }

    /// <inheritdoc/>
    public void Store(ref byte destination, Vector256<byte> c0, Vector256<byte> c1, Vector256<byte> c2, Vector256<byte> c3)
    {
        this.Store(ref destination, c0.GetLower(), c1.GetLower(), c2.GetLower(), c3.GetLower());
        this.Store(ref Unsafe.Add(ref destination, 64), c0.GetUpper(), c1.GetUpper(), c2.GetUpper(), c3.GetUpper());
    }

    /// <inheritdoc/>
    public void Store(ref byte destination, Vector512<byte> c0, Vector512<byte> c1, Vector512<byte> c2, Vector512<byte> c3)
    {
        this.Store(ref destination, c0.GetLower().GetLower(), c1.GetLower().GetLower(), c2.GetLower().GetLower(), c3.GetLower().GetLower());
        this.Store(ref Unsafe.Add(ref destination, 64), c0.GetLower().GetUpper(), c1.GetLower().GetUpper(), c2.GetLower().GetUpper(), c3.GetLower().GetUpper());
        this.Store(ref Unsafe.Add(ref destination, 128), c0.GetUpper().GetLower(), c1.GetUpper().GetLower(), c2.GetUpper().GetLower(), c3.GetUpper().GetLower());
        this.Store(ref Unsafe.Add(ref destination, 192), c0.GetUpper().GetUpper(), c1.GetUpper().GetUpper(), c2.GetUpper().GetUpper(), c3.GetUpper().GetUpper());
    }
}
