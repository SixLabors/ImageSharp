// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads big-endian packed RGBA64 words as four consecutive pixel groups.
/// </summary>
internal readonly struct TiffPacked16NativeSource : ITiffColorSource<ushort>
{
    private static readonly Vector128<byte> ReverseBytes = Vector128.Create((byte)1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffPacked16NativeSource"/> struct.
    /// </summary>
    /// <param name="reverseEndianness">Whether encoded big-endian words differ from host order.</param>
    public TiffPacked16NativeSource(bool reverseEndianness) => this.reverseEndianness = reverseEndianness;

    /// <inheritdoc/>
    public int BytesPerPixel => 8;

    /// <inheritdoc/>
    public int ComponentCount => 4;

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out ushort c0,
        out ushort c1,
        out ushort c2,
        out ushort c3)
    {
        ReadOnlySpan<byte> pixel = source0[(index * 8)..];
        c0 = MemoryMarshal.Read<ushort>(pixel);
        c1 = MemoryMarshal.Read<ushort>(pixel[2..]);
        c2 = MemoryMarshal.Read<ushort>(pixel[4..]);
        c3 = MemoryMarshal.Read<ushort>(pixel[6..]);

        if (this.reverseEndianness)
        {
            c0 = BinaryPrimitives.ReverseEndianness(c0);
            c1 = BinaryPrimitives.ReverseEndianness(c1);
            c2 = BinaryPrimitives.ReverseEndianness(c2);
            c3 = BinaryPrimitives.ReverseEndianness(c3);
        }
    }

    /// <inheritdoc/>
    // Keep the four packed registers in the caller across the source/operator/store pipeline.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector128<ushort> c0,
        out Vector128<ushort> c1,
        out Vector128<ushort> c2,
        out Vector128<ushort> c3)
    {
        ref ushort first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0));
        nuint offset = (uint)index * 4;
        c0 = Vector128.LoadUnsafe(ref first, offset);
        c1 = Vector128.LoadUnsafe(ref first, offset + 8);
        c2 = Vector128.LoadUnsafe(ref first, offset + 16);
        c3 = Vector128.LoadUnsafe(ref first, offset + 24);

        if (this.reverseEndianness)
        {
            c0 = Vector128.ShuffleNative(c0.AsByte(), ReverseBytes).AsUInt16();
            c1 = Vector128.ShuffleNative(c1.AsByte(), ReverseBytes).AsUInt16();
            c2 = Vector128.ShuffleNative(c2.AsByte(), ReverseBytes).AsUInt16();
            c3 = Vector128.ShuffleNative(c3.AsByte(), ReverseBytes).AsUInt16();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<ushort> c0,
        out Vector256<ushort> c1,
        out Vector256<ushort> c2,
        out Vector256<ushort> c3)
    {
        ref ushort first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0));
        nuint offset = (uint)index * 4;
        c0 = Vector256.LoadUnsafe(ref first, offset);
        c1 = Vector256.LoadUnsafe(ref first, offset + 16);
        c2 = Vector256.LoadUnsafe(ref first, offset + 32);
        c3 = Vector256.LoadUnsafe(ref first, offset + 48);

        if (this.reverseEndianness)
        {
            Vector256<byte> mask = Vector256.Create(ReverseBytes, ReverseBytes);
            c0 = Vector256_.ShufflePerLane(c0.AsByte(), mask).AsUInt16();
            c1 = Vector256_.ShufflePerLane(c1.AsByte(), mask).AsUInt16();
            c2 = Vector256_.ShufflePerLane(c2.AsByte(), mask).AsUInt16();
            c3 = Vector256_.ShufflePerLane(c3.AsByte(), mask).AsUInt16();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<ushort> c0,
        out Vector512<ushort> c1,
        out Vector512<ushort> c2,
        out Vector512<ushort> c3)
    {
        ref ushort first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0));
        nuint offset = (uint)index * 4;
        c0 = Vector512.LoadUnsafe(ref first, offset);
        c1 = Vector512.LoadUnsafe(ref first, offset + 32);
        c2 = Vector512.LoadUnsafe(ref first, offset + 64);
        c3 = Vector512.LoadUnsafe(ref first, offset + 96);

        if (this.reverseEndianness)
        {
            Vector512<byte> mask = IShuffle4.ExpandLaneMask(ReverseBytes);
            c0 = Vector512.ShuffleNative(c0.AsByte(), mask).AsUInt16();
            c1 = Vector512.ShuffleNative(c1.AsByte(), mask).AsUInt16();
            c2 = Vector512.ShuffleNative(c2.AsByte(), mask).AsUInt16();
            c3 = Vector512.ShuffleNative(c3.AsByte(), mask).AsUInt16();
        }
    }
}
