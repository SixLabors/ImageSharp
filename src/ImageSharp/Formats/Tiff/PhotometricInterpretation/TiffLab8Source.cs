// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads unsigned lightness and signed chroma from eight-bit TIFF Lab rows.
/// </summary>
internal readonly struct TiffLab8Source : ITiffColorSource<float>
{
    private const float Inv255 = 1F / 255F;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLab8Source"/> struct.
    /// </summary>
    /// <param name="planar">Whether components occupy separate planes.</param>
    public TiffLab8Source(bool planar) => this.planar = planar;

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 1 : 3;

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
        c0 = source0[offset] * 100F * Inv255;
        c1 = (sbyte)(this.planar ? source1[offset] : source0[offset + 1]);
        c2 = (sbyte)(this.planar ? source2[offset] : source0[offset + 2]);
        c3 = 0F;
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
        Vector128<byte> lightness;
        Vector128<byte> chromaA;
        Vector128<byte> chromaB;

        if (this.planar)
        {
            // Four bytes fill the low lanes; the upper lanes are zero padding rather than an out-of-row read.
            lightness = Vector128.Create(MemoryMarshal.Read<uint>(source0.Slice(index)), 0U, 0U, 0U).AsByte();
            chromaA = Vector128.Create(MemoryMarshal.Read<uint>(source1.Slice(index)), 0U, 0U, 0U).AsByte();
            chromaB = Vector128.Create(MemoryMarshal.Read<uint>(source2.Slice(index)), 0U, 0U, 0U).AsByte();
        }
        else
        {
            // Four packed Lab pixels occupy twelve bytes. The last register word is explicit padding.
            ReadOnlySpan<byte> packed = source0.Slice(index * 3);
            Vector128<byte> samples = Vector128.Create(
                MemoryMarshal.Read<uint>(packed),
                MemoryMarshal.Read<uint>(packed.Slice(4)),
                MemoryMarshal.Read<uint>(packed.Slice(8)),
                0U).AsByte();

            lightness = Vector128.Shuffle(samples, Vector128.Create((byte)0, 3, 6, 9, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
            chromaA = Vector128.Shuffle(samples, Vector128.Create((byte)1, 4, 7, 10, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
            chromaB = Vector128.Shuffle(samples, Vector128.Create((byte)2, 5, 8, 11, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16));
        }

        // The TIFF chroma byte is two's-complement signed; widen it as sbyte before float conversion.
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(lightness)).AsUInt32()) * Vector128.Create(100F) * Vector128.Create(Inv255);
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(chromaA.AsSByte())).AsInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(chromaB.AsSByte())).AsInt32());
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
        this.Read(
            source0,
            source1,
            source2,
            source3,
            index,
            out Vector128<float> l0,
            out Vector128<float> a0,
            out Vector128<float> b0,
            out Vector128<float> d0);

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index + 4,
            out Vector128<float> l1,
            out Vector128<float> a1,
            out Vector128<float> b1,
            out Vector128<float> d1);

        c0 = Vector256.Create(l0, l1);
        c1 = Vector256.Create(a0, a1);
        c2 = Vector256.Create(b0, b1);
        c3 = Vector256.Create(d0, d1);
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
            out Vector256<float> l0,
            out Vector256<float> a0,
            out Vector256<float> b0,
            out Vector256<float> d0);

        this.Read(
            source0,
            source1,
            source2,
            source3,
            index + 8,
            out Vector256<float> l1,
            out Vector256<float> a1,
            out Vector256<float> b1,
            out Vector256<float> d1);

        c0 = Vector512.Create(l0, l1);
        c1 = Vector512.Create(a0, a1);
        c2 = Vector512.Create(b0, b1);
        c3 = Vector512.Create(d0, d1);
    }
}
