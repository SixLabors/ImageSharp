// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Expands bounded groups of four 24-bit TIFF integer samples into uint lanes.
/// </summary>
internal static class TiffUInt24Samples
{
    /// <summary>
    /// Gets byte positions that expand four adjacent 24-bit codes into uint lanes.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored code uses big-endian byte order.</param>
    /// <returns>Byte positions with an out-of-range zero byte in each uint lane.</returns>
    public static Vector128<byte> GetPackedShuffleIndices(bool isBigEndian) => isBigEndian
        ? Vector128.Create((byte)2, 1, 0, 16, 5, 4, 3, 16, 8, 7, 6, 16, 11, 10, 9, 16)
        : Vector128.Create((byte)0, 1, 2, 16, 3, 4, 5, 16, 6, 7, 8, 16, 9, 10, 11, 16);

    /// <summary>
    /// Widens four consecutive codes without reading bytes from the next sample group.
    /// </summary>
    /// <param name="source">At least twelve source bytes.</param>
    /// <param name="isBigEndian">Whether each stored code uses big-endian byte order.</param>
    /// <returns>Four integer code lanes.</returns>
    public static Vector128<uint> ReadFour(ReadOnlySpan<byte> source, bool isBigEndian)
    {
        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(source), (ulong)MemoryMarshal.Read<uint>(source.Slice(8))).AsByte();
        Vector128<byte> indices = isBigEndian ? Vector128.Create(
            (byte)2,
            1,
            0,
            12,
            5,
            4,
            3,
            13,
            8,
            7,
            6,
            14,
            11,
            10,
            9,
            15) : Vector128.Create((byte)0, 1, 2, 12, 3, 4, 5, 13, 6, 7, 8, 14, 9, 10, 11, 15);

        // Bytes 12..15 are explicit zeros. Their shuffle positions supply the fourth
        // byte of each uint lane while the other three bytes retain source order.
        return Vector128.ShuffleNative(bytes, indices).AsUInt32();
    }
}
