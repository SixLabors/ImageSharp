// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

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
        Unsafe.Add(ref destination, 1U) = c1;
        Unsafe.Add(ref destination, 2U) = c2;
        Unsafe.Add(ref destination, 3U) = c3;
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
        // The shared interleave keeps the complete 256-bit group in registers and writes consecutive pixels.
        SimdUtils.Interleave4(c0, c1, c2, c3, ref destination);
    }

    /// <inheritdoc/>
    public void Store(ref ushort destination, Vector512<ushort> c0, Vector512<ushort> c1, Vector512<ushort> c2, Vector512<ushort> c3)
    {
        // The shared interleave keeps the complete 512-bit group in registers and writes consecutive pixels.
        SimdUtils.Interleave4(c0, c1, c2, c3, ref destination);
    }
}
