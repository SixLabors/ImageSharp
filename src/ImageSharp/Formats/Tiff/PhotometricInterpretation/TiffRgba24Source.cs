// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four 24-bit integer RGBA components from packed or planar TIFF rows.
/// </summary>
internal readonly struct TiffRgba24Source : ITiffColorSource<float>
{
    private readonly bool isBigEndian;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba24Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored integer uses big-endian byte order.</param>
    /// <param name="planar">Whether the components occupy separate planes.</param>
    public TiffRgba24Source(bool isBigEndian, bool planar)
    {
        this.isBigEndian = isBigEndian;
        this.planar = planar;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 3 : 12;

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
        ReadOnlySpan<byte> red = source0.Slice(offset);
        ReadOnlySpan<byte> green = (this.planar ? source1 : source0).Slice(offset + (this.planar ? 0 : 3));
        ReadOnlySpan<byte> blue = (this.planar ? source2 : source0).Slice(offset + (this.planar ? 0 : 6));
        ReadOnlySpan<byte> alpha = (this.planar ? source3 : source0).Slice(offset + (this.planar ? 0 : 9));
        c0 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(red) : TiffUtilities.ConvertToUInt24LittleEndian(red);
        c1 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(green) : TiffUtilities.ConvertToUInt24LittleEndian(green);
        c2 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(blue) : TiffUtilities.ConvertToUInt24LittleEndian(blue);
        c3 = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(alpha) : TiffUtilities.ConvertToUInt24LittleEndian(alpha);
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
            r = TiffUInt24Samples.ReadFour(source0.Slice(index * 3), this.isBigEndian);
            g = TiffUInt24Samples.ReadFour(source1.Slice(index * 3), this.isBigEndian);
            b = TiffUInt24Samples.ReadFour(source2.Slice(index * 3), this.isBigEndian);
            a = TiffUInt24Samples.ReadFour(source3.Slice(index * 3), this.isBigEndian);
        }
        else
        {
            // Four packed pixels occupy 48 bytes. Three loads start at pixel boundaries;
            // the final load starts four bytes earlier to keep its full 16 bytes in bounds.
            ReadOnlySpan<byte> packed = source0.Slice(index * 12);
            ref byte first = ref MemoryMarshal.GetReference(packed);
            Vector128<byte> indices = TiffUInt24Samples.GetPackedShuffleIndices(this.isBigEndian);
            Vector128<byte> lastIndices = indices + Vector128.Create((byte)4);
            Vector128<uint> pixel0 = Vector128.Shuffle(Vector128.LoadUnsafe(ref first), indices).AsUInt32();
            Vector128<uint> pixel1 = Vector128.Shuffle(Vector128.LoadUnsafe(ref first, (nuint)12), indices).AsUInt32();
            Vector128<uint> pixel2 = Vector128.Shuffle(Vector128.LoadUnsafe(ref first, (nuint)24), indices).AsUInt32();
            Vector128<uint> pixel3 = Vector128.Shuffle(Vector128.LoadUnsafe(ref first, (nuint)32), lastIndices).AsUInt32();

            // Each uint register holds one pixel's RGBA codes. Transposition puts
            // four codes of the same component into each output register.
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
