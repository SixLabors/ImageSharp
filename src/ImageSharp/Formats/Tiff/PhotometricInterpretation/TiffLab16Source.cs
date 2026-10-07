// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads unsigned lightness and signed chroma from sixteen-bit TIFF Lab rows.
/// </summary>
internal readonly struct TiffLab16Source : ITiffColorSource<float>
{
    private const float Inv65535 = 1F / 65535F;
    private const float ChromaScale = 1F / 256F;
    private readonly bool reverseEndianness;
    private readonly bool planar;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLab16Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether the stored words use big-endian byte order.</param>
    /// <param name="planar">Whether components occupy separate planes.</param>
    public TiffLab16Source(bool isBigEndian, bool planar)
    {
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.planar = planar;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.planar ? 2 : 6;

    /// <inheritdoc />
    public int ComponentCount => 3;

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out float c0, out float c1, out float c2, out float c3)
    {
        int offset = index * this.BytesPerPixel;
        ushort l = MemoryMarshal.Read<ushort>(source0.Slice(offset));
        ushort a = MemoryMarshal.Read<ushort>((this.planar ? source1 : source0).Slice(offset + (this.planar ? 0 : 2)));
        ushort b = MemoryMarshal.Read<ushort>((this.planar ? source2 : source0).Slice(offset + (this.planar ? 0 : 4)));

        if (this.reverseEndianness)
        {
            l = BinaryPrimitives.ReverseEndianness(l);
            a = BinaryPrimitives.ReverseEndianness(a);
            b = BinaryPrimitives.ReverseEndianness(b);
        }

        // L* uses the full unsigned code range; a* and b* use signed fixed-point units of 1/256.
        c0 = l * 100F * Inv65535;
        c1 = unchecked((short)a) * ChromaScale;
        c2 = unchecked((short)b) * ChromaScale;
        c3 = 0F;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        Vector128<ushort> lightness;
        Vector128<ushort> chromaA;
        Vector128<ushort> chromaB;

        if (this.planar)
        {
            // Four words from each plane fill the low half; the high half is padding.
            lightness = Vector128.Create(MemoryMarshal.Read<ulong>(source0.Slice(index * 2)), 0UL).AsUInt16();
            chromaA = Vector128.Create(MemoryMarshal.Read<ulong>(source1.Slice(index * 2)), 0UL).AsUInt16();
            chromaB = Vector128.Create(MemoryMarshal.Read<ulong>(source2.Slice(index * 2)), 0UL).AsUInt16();
        }
        else
        {
            // The first sixteen bytes contain two whole pixels and L2/A2; the last eight contain B2 and pixel three.
            ReadOnlySpan<byte> packed = source0.Slice(index * 6);
            Vector128<ushort> first = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(packed)));
            Vector128<ushort> last = Vector128.Create(MemoryMarshal.Read<ulong>(packed.Slice(16)), 0UL).AsUInt16();
            lightness = Vector128.Shuffle(first, Vector128.Create((ushort)0, 3, 6, 8, 8, 8, 8, 8)) | Vector128.Shuffle(last, Vector128.Create((ushort)8, 8, 8, 1, 8, 8, 8, 8));
            chromaA = Vector128.Shuffle(first, Vector128.Create((ushort)1, 4, 7, 8, 8, 8, 8, 8)) | Vector128.Shuffle(last, Vector128.Create((ushort)8, 8, 8, 2, 8, 8, 8, 8));
            chromaB = Vector128.Shuffle(first, Vector128.Create((ushort)2, 5, 8, 8, 8, 8, 8, 8)) | Vector128.Shuffle(last, Vector128.Create((ushort)8, 8, 0, 3, 8, 8, 8, 8));
        }

        if (this.reverseEndianness)
        {
            // Swap bytes within each word before signed widening; doing it afterward loses the sign bit.
            lightness = (lightness >> 8) | (lightness << 8);
            chromaA = (chromaA >> 8) | (chromaA << 8);
            chromaB = (chromaB >> 8) | (chromaB << 8);
        }

        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(lightness)) * Vector128.Create(100F) * Vector128.Create(Inv65535);
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(chromaA.AsInt16())) * Vector128.Create(ChromaScale);
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(chromaB.AsInt16())) * Vector128.Create(ChromaScale);
        c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector128<float> l0, out Vector128<float> a0, out Vector128<float> b0, out Vector128<float> d0);
        this.Read(source0, source1, source2, source3, index + 4, out Vector128<float> l1, out Vector128<float> a1, out Vector128<float> b1, out Vector128<float> d1);
        c0 = Vector256.Create(l0, l1);
        c1 = Vector256.Create(a0, a1);
        c2 = Vector256.Create(b0, b1);
        c3 = Vector256.Create(d0, d1);
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector256<float> l0, out Vector256<float> a0, out Vector256<float> b0, out Vector256<float> d0);
        this.Read(source0, source1, source2, source3, index + 8, out Vector256<float> l1, out Vector256<float> a1, out Vector256<float> b1, out Vector256<float> d1);
        c0 = Vector512.Create(l0, l1);
        c1 = Vector512.Create(a0, a1);
        c2 = Vector512.Create(b0, b1);
        c3 = Vector512.Create(d0, d1);
    }
}
