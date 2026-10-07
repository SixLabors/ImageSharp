// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads packed or planar eight-bit TIFF YCbCr component codes.
/// </summary>
internal readonly struct TiffYCbCr8Source : ITiffColorSource<float>
{
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffYCbCr8Source"/> struct.
    /// </summary>
    /// <param name="planar">Whether the three components occupy separate planes.</param>
    public TiffYCbCr8Source(bool planar) => this.planar = planar;

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 1 : 3;

    /// <inheritdoc />
    public int ComponentCount => 3;

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out float c0, out float c1, out float c2, out float c3)
    {
        int offset = index * this.BytesPerPixel;
        c0 = source0[offset];
        c1 = (this.planar ? source1 : source0)[offset + (this.planar ? 0 : 1)];
        c2 = (this.planar ? source2 : source0)[offset + (this.planar ? 0 : 2)];
        c3 = 0F;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        Vector128<byte> y;
        Vector128<byte> cb;
        Vector128<byte> cr;

        if (this.planar)
        {
            // Four codes fill the low lanes; explicit zero padding avoids reading into the next row.
            y = Vector128.Create(MemoryMarshal.Read<uint>(source0.Slice(index)), 0U, 0U, 0U).AsByte();
            cb = Vector128.Create(MemoryMarshal.Read<uint>(source1.Slice(index)), 0U, 0U, 0U).AsByte();
            cr = Vector128.Create(MemoryMarshal.Read<uint>(source2.Slice(index)), 0U, 0U, 0U).AsByte();
        }
        else
        {
            // Four packed pixels occupy exactly twelve bytes; the last word is zero padding.
            ReadOnlySpan<byte> packed = source0.Slice(index * 3);
            Vector128<byte> samples = Vector128.Create(MemoryMarshal.Read<uint>(packed), MemoryMarshal.Read<uint>(packed.Slice(4)), MemoryMarshal.Read<uint>(packed.Slice(8)), 0U).AsByte();
            y = Vector128.Shuffle(samples, Vector128.Create((byte)0, 3, 6, 9, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
            cb = Vector128.Shuffle(samples, Vector128.Create((byte)1, 4, 7, 10, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
            cr = Vector128.Shuffle(samples, Vector128.Create((byte)2, 5, 8, 11, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
        }

        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(y)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(cb)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(cr)).AsUInt32());
        c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector128<float> y0, out Vector128<float> cb0, out Vector128<float> cr0, out Vector128<float> a0);
        this.Read(source0, source1, source2, source3, index + 4, out Vector128<float> y1, out Vector128<float> cb1, out Vector128<float> cr1, out Vector128<float> a1);
        c0 = Vector256.Create(y0, y1);
        c1 = Vector256.Create(cb0, cb1);
        c2 = Vector256.Create(cr0, cr1);
        c3 = Vector256.Create(a0, a1);
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector256<float> y0, out Vector256<float> cb0, out Vector256<float> cr0, out Vector256<float> a0);
        this.Read(source0, source1, source2, source3, index + 8, out Vector256<float> y1, out Vector256<float> cb1, out Vector256<float> cr1, out Vector256<float> a1);
        c0 = Vector512.Create(y0, y1);
        c1 = Vector512.Create(cb0, cb1);
        c2 = Vector512.Create(cr0, cr1);
        c3 = Vector512.Create(a0, a1);
    }
}
