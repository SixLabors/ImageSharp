// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Expands bounded groups of 24-bit TIFF integer samples into uint lanes.
/// </summary>
internal static class TiffUInt24Samples
{
    /// <summary>
    /// Gets byte positions that expand four adjacent 24-bit codes into uint lanes.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored code uses big-endian byte order.</param>
    /// <returns>Byte positions with an out-of-range zero byte in each uint lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> GetPackedShuffleIndices(bool isBigEndian) => isBigEndian
        ? Vector128.Create((byte)2, 1, 0, 128, 5, 4, 3, 128, 8, 7, 6, 128, 11, 10, 9, 128)
        : Vector128.Create((byte)0, 1, 2, 128, 3, 4, 5, 128, 6, 7, 8, 128, 9, 10, 11, 128);

    /// <summary>
    /// Widens four consecutive codes without reading bytes from the next sample group.
    /// </summary>
    /// <param name="source">At least twelve source bytes.</param>
    /// <param name="isBigEndian">Whether each stored code uses big-endian byte order.</param>
    /// <returns>Four integer code lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> ReadFour(ReadOnlySpan<byte> source, bool isBigEndian)
    {
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(source), (ulong)MemoryMarshal.Read<uint>(source.Slice(8))).AsByte();

        // Expand four three-byte samples into four uint lanes. Reverse each sample's bytes for big-endian input.
        // Each fourth mask index selects one of the zero bytes, so expansion changes neither the pixel order nor the sample value.
        Vector128<byte> indices = isBigEndian
            ? Vector128.Create((byte)2, 1, 0, 12, 5, 4, 3, 13, 8, 7, 6, 14, 11, 10, 9, 15)
            : Vector128.Create((byte)0, 1, 2, 12, 3, 4, 5, 13, 6, 7, 8, 14, 9, 10, 11, 15);

        // Bytes 12..15 are explicit zeros. Their shuffle positions supply the fourth
        // byte of each uint lane while the other three bytes retain source order.
        return Vector128.ShuffleNative(bytes, indices).AsUInt32();
    }

    /// <summary>
    /// Expands eight adjacent 24-bit codes into integer lanes without reading beyond the group.
    /// </summary>
    /// <param name="source">At least twenty-four source bytes.</param>
    /// <param name="isBigEndian">Whether each code uses big-endian byte order.</param>
    /// <param name="values">The eight expanded codes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Read(ReadOnlySpan<byte> source, bool isBigEndian, out Vector256<uint> values)
    {
        // Two overlapping loads cover exactly twenty-four bytes. Each shuffle expands four three-byte codes.
        // Index 128 has its high bit set, so the native byte shuffle supplies the zero fourth byte of each uint.
        ref byte first = ref MemoryMarshal.GetReference(source);
        Vector128<byte> indices = GetPackedShuffleIndices(isBigEndian);
        Vector128<uint> lower = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first), indices).AsUInt32();
        Vector128<uint> upper = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first, 8U), indices + Vector128.Create((byte)4)).AsUInt32();
        values = Vector256.Create(lower, upper);
    }

    /// <summary>
    /// Expands sixteen adjacent 24-bit codes into integer lanes without reading beyond the group.
    /// </summary>
    /// <param name="source">At least forty-eight source bytes.</param>
    /// <param name="isBigEndian">Whether each code uses big-endian byte order.</param>
    /// <param name="values">The sixteen expanded codes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Read(ReadOnlySpan<byte> source, bool isBigEndian, out Vector512<uint> values)
    {
        // Expand the two bounded byte groups before any numeric conversion. Callers convert and process all sixteen lanes together.
        Read(source, isBigEndian, out Vector256<uint> lower);
        Read(source[24..], isBigEndian, out Vector256<uint> upper);
        values = Vector512.Create(lower, upper);
    }
}
