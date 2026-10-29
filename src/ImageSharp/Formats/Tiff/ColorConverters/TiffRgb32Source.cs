// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads three 32-bit integer RGB components from packed or planar TIFF rows.
/// </summary>
internal readonly struct TiffRgb32Source : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb32Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored integer uses big-endian byte order.</param>
    /// <param name="planar">Whether the components occupy separate planes.</param>
    public TiffRgb32Source(bool isBigEndian, bool planar)
    {
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.planar = planar;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 4 : 12;

    /// <inheritdoc />
    public int ComponentCount => 3;

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out float c0,
        out float c1,
        out float c2,
        out float c3)
    {
        int offset = index * this.BytesPerPixel;
        uint r = MemoryMarshal.Read<uint>(source0.Slice(offset));
        uint g = MemoryMarshal.Read<uint>((this.planar ? source1 : source0).Slice(offset + (this.planar ? 0 : 4)));
        uint b = MemoryMarshal.Read<uint>((this.planar ? source2 : source0).Slice(offset + (this.planar ? 0 : 8)));

        if (this.reverseEndianness)
        {
            r = BinaryPrimitives.ReverseEndianness(r);
            g = BinaryPrimitives.ReverseEndianness(g);
            b = BinaryPrimitives.ReverseEndianness(b);
        }

        c0 = r;
        c1 = g;
        c2 = b;
        c3 = 0;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector128<float> c0,
        out Vector128<float> c1,
        out Vector128<float> c2,
        out Vector128<float> c3)
    {
        Vector128<float> red;
        Vector128<float> green;
        Vector128<float> blue;

        if (this.planar)
        {
            red = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source0)), (nuint)index);
            green = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source1)), (nuint)index);
            blue = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source2)), (nuint)index);
        }
        else
        {
            // The shared deinterleave loads a complete group before changing lane order. Float views preserve the integer bits.
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out red, out green, out blue);
        }

        if (this.reverseEndianness)
        {
            // Swap bytes within each integer before uint-to-float conversion.
            red = WZYXShuffle4.Invoke(red.AsByte()).AsSingle();
            green = WZYXShuffle4.Invoke(green.AsByte()).AsSingle();
            blue = WZYXShuffle4.Invoke(blue.AsByte()).AsSingle();
        }

        c0 = Vector128.ConvertToSingle(red.AsUInt32());
        c1 = Vector128.ConvertToSingle(green.AsUInt32());
        c2 = Vector128.ConvertToSingle(blue.AsUInt32());
        c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector256<float> c0,
        out Vector256<float> c1,
        out Vector256<float> c2,
        out Vector256<float> c3)
    {
        Vector256<float> red;
        Vector256<float> green;
        Vector256<float> blue;

        if (this.planar)
        {
            red = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source0)), (nuint)index);
            green = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source1)), (nuint)index);
            blue = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source2)), (nuint)index);
        }
        else
        {
            // The shared deinterleave loads a complete group before changing lane order. Float views preserve the integer bits.
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out red, out green, out blue);
        }

        if (this.reverseEndianness)
        {
            // Swap bytes within each integer before uint-to-float conversion.
            red = WZYXShuffle4.Invoke(red.AsByte()).AsSingle();
            green = WZYXShuffle4.Invoke(green.AsByte()).AsSingle();
            blue = WZYXShuffle4.Invoke(blue.AsByte()).AsSingle();
        }

        c0 = Vector256.ConvertToSingle(red.AsUInt32());
        c1 = Vector256.ConvertToSingle(green.AsUInt32());
        c2 = Vector256.ConvertToSingle(blue.AsUInt32());
        c3 = Vector256<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(
        ReadOnlySpan<byte> source0,
        ReadOnlySpan<byte> source1,
        ReadOnlySpan<byte> source2,
        ReadOnlySpan<byte> source3,
        int index,
        out Vector512<float> c0,
        out Vector512<float> c1,
        out Vector512<float> c2,
        out Vector512<float> c3)
    {
        Vector512<float> red;
        Vector512<float> green;
        Vector512<float> blue;

        if (this.planar)
        {
            red = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source0)), (nuint)index);
            green = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source1)), (nuint)index);
            blue = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(source2)), (nuint)index);
        }
        else
        {
            // The shared deinterleave loads a complete group before changing lane order. Float views preserve the integer bits.
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out red, out green, out blue);
        }

        if (this.reverseEndianness)
        {
            // Swap bytes within each integer before uint-to-float conversion.
            red = WZYXShuffle4.Invoke(red.AsByte()).AsSingle();
            green = WZYXShuffle4.Invoke(green.AsByte()).AsSingle();
            blue = WZYXShuffle4.Invoke(blue.AsByte()).AsSingle();
        }

        c0 = Vector512.ConvertToSingle(red.AsUInt32());
        c1 = Vector512.ConvertToSingle(green.AsUInt32());
        c2 = Vector512.ConvertToSingle(blue.AsUInt32());
        c3 = Vector512<float>.Zero;
    }
}
