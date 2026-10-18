// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads three 24-bit integer RGB components from packed or planar TIFF rows.
/// </summary>
internal readonly struct TiffRgb24Source : ITiffColorSource<float>
{
    private readonly bool isBigEndian;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb24Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored integer uses big-endian byte order.</param>
    /// <param name="planar">Whether the components occupy separate planes.</param>
    public TiffRgb24Source(bool isBigEndian, bool planar)
    {
        this.isBigEndian = isBigEndian;
        this.planar = planar;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 3 : 9;

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
        ReadOnlySpan<byte> red = source0.Slice(offset);
        ReadOnlySpan<byte> green = (this.planar ? source1 : source0).Slice(offset + (this.planar ? 0 : 3));
        ReadOnlySpan<byte> blue = (this.planar ? source2 : source0).Slice(offset + (this.planar ? 0 : 6));
        c0 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(red) : TiffUtilities.ConvertToUInt24LittleEndian(red);
        c1 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(green) : TiffUtilities.ConvertToUInt24LittleEndian(green);
        c2 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(blue) : TiffUtilities.ConvertToUInt24LittleEndian(blue);
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
        Vector128<uint> r;
        Vector128<uint> g;
        Vector128<uint> b;

        if (this.planar)
        {
            // Four 24-bit codes occupy twelve bytes. The final four bytes in the register
            // are explicit zero padding, so a row ending at this group stays in bounds.
            r = TiffUInt24Samples.ReadFour(source0.Slice(index * 3), this.isBigEndian);
            g = TiffUInt24Samples.ReadFour(source1.Slice(index * 3), this.isBigEndian);
            b = TiffUInt24Samples.ReadFour(source2.Slice(index * 3), this.isBigEndian);
        }
        else
        {
            // Four packed pixels occupy 36 bytes. Each vector starts at one pixel's
            // first byte, so a byte shuffle widens its RGB codes without uint gathers.
            // The last 16-byte load starts seven bytes before pixel 3 to stay in row bounds.
            ReadOnlySpan<byte> packed = source0.Slice(index * 9);
            ref byte first = ref MemoryMarshal.GetReference(packed);
            Vector128<byte> indices = TiffUInt24Samples.GetPackedShuffleIndices(this.isBigEndian);
            Vector128<byte> lastIndices = indices + Vector128.Create((byte)7);
            Vector128<uint> pixel0 = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first), indices).AsUInt32();
            Vector128<uint> pixel1 = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first, (nuint)9), indices).AsUInt32();
            Vector128<uint> pixel2 = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first, (nuint)18), indices).AsUInt32();
            Vector128<uint> pixel3 = Vector128.ShuffleNative(Vector128.LoadUnsafe(ref first, (nuint)20), lastIndices).AsUInt32();

            // Each source register has one pixel's RGB codes in its first three uint
            // lanes. Transposition makes each output register one component across four pixels.
            SimdUtils.Transpose4(
                pixel0.AsSingle(),
                pixel1.AsSingle(),
                pixel2.AsSingle(),
                pixel3.AsSingle(),
                out Vector128<float> red,
                out Vector128<float> green,
                out Vector128<float> blue,
                out _);

            r = red.AsUInt32();
            g = green.AsUInt32();
            b = blue.AsUInt32();
        }

        c0 = Vector128.ConvertToSingle(r);
        c1 = Vector128.ConvertToSingle(g);
        c2 = Vector128.ConvertToSingle(b);
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
        Vector256<uint> red;
        Vector256<uint> green;
        Vector256<uint> blue;

        if (this.planar)
        {
            TiffUInt24Samples.Read(source0[(index * 3)..], this.isBigEndian, out red);
            TiffUInt24Samples.Read(source1[(index * 3)..], this.isBigEndian, out green);
            TiffUInt24Samples.Read(source2[(index * 3)..], this.isBigEndian, out blue);
        }
        else
        {
            // Expand adjacent codes before deinterleaving. Integer-to-float conversion happens once per full component register.
            ReadOnlySpan<byte> packed = source0[(index * 9)..];
            TiffUInt24Samples.Read(packed, this.isBigEndian, out Vector256<uint> group0);
            TiffUInt24Samples.Read(packed[24..], this.isBigEndian, out Vector256<uint> group1);
            TiffUInt24Samples.Read(packed[48..], this.isBigEndian, out Vector256<uint> group2);

            SimdUtils.Deinterleave3(
                group0.AsSingle(),
                group1.AsSingle(),
                group2.AsSingle(),
                out Vector256<float> r,
                out Vector256<float> g,
                out Vector256<float> b);

            red = r.AsUInt32();
            green = g.AsUInt32();
            blue = b.AsUInt32();
        }

        c0 = Vector256.ConvertToSingle(red);
        c1 = Vector256.ConvertToSingle(green);
        c2 = Vector256.ConvertToSingle(blue);
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
        Vector512<uint> red;
        Vector512<uint> green;
        Vector512<uint> blue;

        if (this.planar)
        {
            TiffUInt24Samples.Read(source0[(index * 3)..], this.isBigEndian, out red);
            TiffUInt24Samples.Read(source1[(index * 3)..], this.isBigEndian, out green);
            TiffUInt24Samples.Read(source2[(index * 3)..], this.isBigEndian, out blue);
        }
        else
        {
            // Expand adjacent codes before deinterleaving. Integer-to-float conversion happens once per full component register.
            ReadOnlySpan<byte> packed = source0[(index * 9)..];
            TiffUInt24Samples.Read(packed, this.isBigEndian, out Vector512<uint> group0);
            TiffUInt24Samples.Read(packed[48..], this.isBigEndian, out Vector512<uint> group1);
            TiffUInt24Samples.Read(packed[96..], this.isBigEndian, out Vector512<uint> group2);

            SimdUtils.Deinterleave3(
                group0.AsSingle(),
                group1.AsSingle(),
                group2.AsSingle(),
                out Vector512<float> r,
                out Vector512<float> g,
                out Vector512<float> b);

            red = r.AsUInt32();
            green = g.AsUInt32();
            blue = b.AsUInt32();
        }

        c0 = Vector512.ConvertToSingle(red);
        c1 = Vector512.ConvertToSingle(green);
        c2 = Vector512.ConvertToSingle(blue);
        c3 = Vector512<float>.Zero;
    }
}
