// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads native ushort components from three or four TIFF planes.
/// </summary>
internal readonly struct TiffPlanar16NativeSource : ITiffColorSource<ushort>
{
    private static readonly Vector128<byte> ReverseBytes = Vector128.Create((byte)1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);
    private readonly bool reverseEndianness;
    private readonly int componentCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffPlanar16NativeSource"/> struct.
    /// </summary>
    /// <param name="reverseEndianness">Whether source word order differs from host order.</param>
    /// <param name="componentCount">The number of populated component planes.</param>
    public TiffPlanar16NativeSource(bool reverseEndianness, int componentCount)
    {
        this.reverseEndianness = reverseEndianness;
        this.componentCount = componentCount;
    }

    /// <inheritdoc/>
    public int BytesPerPixel => 2;

    /// <inheritdoc/>
    public int ComponentCount => this.componentCount;

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
        int offset = index * sizeof(ushort);
        c0 = MemoryMarshal.Read<ushort>(source0[offset..]);
        c1 = MemoryMarshal.Read<ushort>(source1[offset..]);
        c2 = MemoryMarshal.Read<ushort>(source2[offset..]);
        c3 = this.componentCount == 4 ? MemoryMarshal.Read<ushort>(source3[offset..]) : default;

        if (this.reverseEndianness)
        {
            c0 = BinaryPrimitives.ReverseEndianness(c0);
            c1 = BinaryPrimitives.ReverseEndianness(c1);
            c2 = BinaryPrimitives.ReverseEndianness(c2);
            c3 = BinaryPrimitives.ReverseEndianness(c3);
        }
    }

    /// <inheritdoc/>
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
        c0 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
        c1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
        c2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source3)), (nuint)index) : default;

        if (this.reverseEndianness)
        {
            c0 = Vector128.ShuffleNative(c0.AsByte(), ReverseBytes).AsUInt16();
            c1 = Vector128.ShuffleNative(c1.AsByte(), ReverseBytes).AsUInt16();
            c2 = Vector128.ShuffleNative(c2.AsByte(), ReverseBytes).AsUInt16();
            c3 = Vector128.ShuffleNative(c3.AsByte(), ReverseBytes).AsUInt16();
        }
    }

    /// <inheritdoc/>
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
        c0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
        c1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
        c2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source3)), (nuint)index) : default;

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
        c0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
        c1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
        c2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source3)), (nuint)index) : default;

        if (this.reverseEndianness)
        {
            // Native AVX-512 shuffles index each 128-bit lane; the expanded mask
            // also preserves these lanes on the managed absolute-index fallback.
            Vector512<byte> mask = IShuffle4.ExpandLaneMask(ReverseBytes);
            c0 = Vector512.ShuffleNative(c0.AsByte(), mask).AsUInt16();
            c1 = Vector512.ShuffleNative(c1.AsByte(), mask).AsUInt16();
            c2 = Vector512.ShuffleNative(c2.AsByte(), mask).AsUInt16();
            c3 = Vector512.ShuffleNative(c3.AsByte(), mask).AsUInt16();
        }
    }
}
