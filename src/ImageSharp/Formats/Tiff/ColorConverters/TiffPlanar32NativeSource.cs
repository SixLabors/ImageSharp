// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads exact native uint components from three or four TIFF planes.
/// </summary>
internal readonly struct TiffPlanar32NativeSource : ITiffColorSource<uint>
{
    private readonly bool reverseEndianness;
    private readonly int componentCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffPlanar32NativeSource"/> struct.
    /// </summary>
    /// <param name="reverseEndianness">Whether source word order differs from host order.</param>
    /// <param name="componentCount">The number of populated component planes.</param>
    public TiffPlanar32NativeSource(bool reverseEndianness, int componentCount)
    {
        this.reverseEndianness = reverseEndianness;
        this.componentCount = componentCount;
    }

    /// <inheritdoc/>
    public int BytesPerPixel => 4;

    /// <inheritdoc/>
    public int ComponentCount => this.componentCount;

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out uint c0,
        out uint c1,
        out uint c2,
        out uint c3)
    {
        int offset = index * sizeof(uint);
        c0 = MemoryMarshal.Read<uint>(source0[offset..]);
        c1 = MemoryMarshal.Read<uint>(source1[offset..]);
        c2 = MemoryMarshal.Read<uint>(source2[offset..]);
        c3 = this.componentCount == 4 ? MemoryMarshal.Read<uint>(source3[offset..]) : default;

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
        out Vector128<uint> c0,
        out Vector128<uint> c1,
        out Vector128<uint> c2,
        out Vector128<uint> c3)
    {
        c0 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
        c2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index) : default;

        if (this.reverseEndianness)
        {
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsUInt32();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsUInt32();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsUInt32();
            c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsUInt32();
        }
    }

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<uint> c0,
        out Vector256<uint> c1,
        out Vector256<uint> c2,
        out Vector256<uint> c3)
    {
        c0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
        c2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index) : default;

        if (this.reverseEndianness)
        {
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsUInt32();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsUInt32();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsUInt32();
            c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsUInt32();
        }
    }

    /// <inheritdoc/>
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<uint> c0,
        out Vector512<uint> c1,
        out Vector512<uint> c2,
        out Vector512<uint> c3)
    {
        c0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
        c2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
        c3 = this.componentCount == 4 ? Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index) : default;

        if (this.reverseEndianness)
        {
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsUInt32();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsUInt32();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsUInt32();
            c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsUInt32();
        }
    }
}
