// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four 32-bit integer RGBA components from packed or planar TIFF rows.
/// </summary>
internal readonly struct TiffRgba32Source : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba32Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored integer uses big-endian byte order.</param>
    /// <param name="planar">Whether the components occupy separate planes.</param>
    public TiffRgba32Source(bool isBigEndian, bool planar)
    {
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.planar = planar;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 4 : 16;

    /// <inheritdoc />
    public int ComponentCount => 4;

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
        uint a = MemoryMarshal.Read<uint>((this.planar ? source3 : source0).Slice(offset + (this.planar ? 0 : 12)));

        if (this.reverseEndianness)
        {
            r = BinaryPrimitives.ReverseEndianness(r);
            g = BinaryPrimitives.ReverseEndianness(g);
            b = BinaryPrimitives.ReverseEndianness(b);
            a = BinaryPrimitives.ReverseEndianness(a);
        }

        c0 = r;
        c1 = g;
        c2 = b;
        c3 = a;
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
        Vector128<uint> r;
        Vector128<uint> g;
        Vector128<uint> b;
        Vector128<uint> a;

        if (this.planar)
        {
            r = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
            g = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
            b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
            a = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index);
        }
        else
        {
            // The packed group contains exactly four complete RGBA pixels, so each
            // register can be loaded without reading beyond the supplied row.
            ref uint first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0));
            ref uint group = ref Unsafe.Add(ref first, index * 4);
            Vector128<uint> pixel0 = Unsafe.As<uint, Vector128<uint>>(ref group);
            Vector128<uint> pixel1 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 4));
            Vector128<uint> pixel2 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 8));
            Vector128<uint> pixel3 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 12));
            SimdUtils.Transpose4(
                pixel0.AsSingle(),
                pixel1.AsSingle(),
                pixel2.AsSingle(),
                pixel3.AsSingle(),
                out Vector128<float> red,
                out Vector128<float> green,
                out Vector128<float> blue,
                out Vector128<float> alpha);

            r = red.AsUInt32();
            g = green.AsUInt32();
            b = blue.AsUInt32();
            a = alpha.AsUInt32();
        }

        if (this.reverseEndianness)
        {
            // Swap bytes inside each integer lane before converting the uint value to float.
            r = WZYXShuffle4.Invoke(r.AsByte()).AsUInt32();
            g = WZYXShuffle4.Invoke(g.AsByte()).AsUInt32();
            b = WZYXShuffle4.Invoke(b.AsByte()).AsUInt32();
            a = WZYXShuffle4.Invoke(a.AsByte()).AsUInt32();
        }

        c0 = Vector128.ConvertToSingle(r);
        c1 = Vector128.ConvertToSingle(g);
        c2 = Vector128.ConvertToSingle(b);
        c3 = Vector128.ConvertToSingle(a);
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
        if (this.planar)
        {
            Vector256<uint> r = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
            Vector256<uint> g = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
            Vector256<uint> b = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
            Vector256<uint> a = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index);

            if (this.reverseEndianness)
            {
                r = WZYXShuffle4.Invoke(r.AsByte()).AsUInt32();
                g = WZYXShuffle4.Invoke(g.AsByte()).AsUInt32();
                b = WZYXShuffle4.Invoke(b.AsByte()).AsUInt32();
                a = WZYXShuffle4.Invoke(a.AsByte()).AsUInt32();
            }

            c0 = Vector256.ConvertToSingle(r);
            c1 = Vector256.ConvertToSingle(g);
            c2 = Vector256.ConvertToSingle(b);
            c3 = Vector256.ConvertToSingle(a);
            return;
        }

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index,
            out Vector128<float> r0,
            out Vector128<float> g0,
            out Vector128<float> b0,
            out Vector128<float> a0);

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index + 4,
            out Vector128<float> r1,
            out Vector128<float> g1,
            out Vector128<float> b1,
            out Vector128<float> a1);

        c0 = Vector256.Create(r0, r1);
        c1 = Vector256.Create(g0, g1);
        c2 = Vector256.Create(b0, b1);
        c3 = Vector256.Create(a0, a1);
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
        if (this.planar)
        {
            Vector512<uint> r = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
            Vector512<uint> g = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source1)), (nuint)index);
            Vector512<uint> b = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source2)), (nuint)index);
            Vector512<uint> a = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source3)), (nuint)index);

            if (this.reverseEndianness)
            {
                r = WZYXShuffle4.Invoke(r.AsByte()).AsUInt32();
                g = WZYXShuffle4.Invoke(g.AsByte()).AsUInt32();
                b = WZYXShuffle4.Invoke(b.AsByte()).AsUInt32();
                a = WZYXShuffle4.Invoke(a.AsByte()).AsUInt32();
            }

            c0 = Vector512.ConvertToSingle(r);
            c1 = Vector512.ConvertToSingle(g);
            c2 = Vector512.ConvertToSingle(b);
            c3 = Vector512.ConvertToSingle(a);
            return;
        }

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index,
            out Vector256<float> r0,
            out Vector256<float> g0,
            out Vector256<float> b0,
            out Vector256<float> a0);

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index + 8,
            out Vector256<float> r1,
            out Vector256<float> g1,
            out Vector256<float> b1,
            out Vector256<float> a1);

        c0 = Vector512.Create(r0, r1);
        c1 = Vector512.Create(g0, g1);
        c2 = Vector512.Create(b0, b1);
        c3 = Vector512.Create(a0, a1);
    }
}
