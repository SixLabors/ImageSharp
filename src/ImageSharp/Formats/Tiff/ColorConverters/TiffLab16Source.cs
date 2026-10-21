// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

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
        Vector64<ushort> lightness;
        Vector64<ushort> chromaA;
        Vector64<ushort> chromaB;

        if (this.planar)
        {
            // Four words from each plane fill exactly one 64-bit register.
            lightness = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
            chromaA = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
            chromaB = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, ushort>(source0), index, out lightness, out chromaA, out chromaB);
        }

        if (this.reverseEndianness)
        {
            // Correct byte order before interpreting the two chroma channels as signed fixed-point values.
            lightness = (lightness >> 8) | (lightness << 8);
            chromaA = (chromaA >> 8) | (chromaA << 8);
            chromaB = (chromaB >> 8) | (chromaB << 8);
        }

        // L uses unsigned codes over [0,65535] for [0,100]. Chroma uses signed words in units of 1/256.
        // Widening after the signed reinterpretation preserves negative a and b. Each output lane retains its source pixel.
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(lightness.ToVector128())) * Vector128.Create(100F) * Vector128.Create(Inv65535);
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(chromaA.AsInt16().ToVector128())) * Vector128.Create(ChromaScale);
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(chromaB.AsInt16().ToVector128())) * Vector128.Create(ChromaScale);
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
        Vector128<ushort> lightness;
        Vector128<ushort> chromaA;
        Vector128<ushort> chromaB;

        if (this.planar)
        {
            // One 128-bit load supplies 8 words per component. Widen only after byte order is corrected.
            lightness = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
            chromaA = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
            chromaB = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, ushort>(source0), index, out lightness, out chromaA, out chromaB);
        }

        if (this.reverseEndianness)
        {
            // Signed chroma must be widened after the two bytes of each word have been reversed.
            lightness = (lightness >> 8) | (lightness << 8);
            chromaA = (chromaA >> 8) | (chromaA << 8);
            chromaB = (chromaB >> 8) | (chromaB << 8);
        }

        // L uses unsigned codes over [0,65535] for [0,100]. Chroma uses signed words in units of 1/256.
        // Widening after the signed reinterpretation preserves negative a and b. Each output lane retains its source pixel.
        c0 = Vector256.ConvertToSingle(Vector256.WidenLower(lightness.ToVector256())) * Vector256.Create(100F) * Vector256.Create(Inv65535);
        c1 = Vector256.ConvertToSingle(Vector256.WidenLower(chromaA.AsInt16().ToVector256())) * Vector256.Create(ChromaScale);
        c2 = Vector256.ConvertToSingle(Vector256.WidenLower(chromaB.AsInt16().ToVector256())) * Vector256.Create(ChromaScale);
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
        Vector256<ushort> lightness;
        Vector256<ushort> chromaA;
        Vector256<ushort> chromaB;

        if (this.planar)
        {
            // One 256-bit load supplies 16 words per component. Widen only after byte order is corrected.
            lightness = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source0)), (nuint)index);
            chromaA = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source1)), (nuint)index);
            chromaB = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ushort>(source2)), (nuint)index);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, ushort>(source0), index, out lightness, out chromaA, out chromaB);
        }

        if (this.reverseEndianness)
        {
            // Signed chroma must be widened after the two bytes of each word have been reversed.
            lightness = (lightness >> 8) | (lightness << 8);
            chromaA = (chromaA >> 8) | (chromaA << 8);
            chromaB = (chromaB >> 8) | (chromaB << 8);
        }

        // L uses unsigned codes over [0,65535] for [0,100]. Chroma uses signed words in units of 1/256.
        // Widening after the signed reinterpretation preserves negative a and b. Each output lane retains its source pixel.
        c0 = Vector512.ConvertToSingle(Vector512.WidenLower(lightness.ToVector512())) * Vector512.Create(100F) * Vector512.Create(Inv65535);
        c1 = Vector512.ConvertToSingle(Vector512.WidenLower(chromaA.AsInt16().ToVector512())) * Vector512.Create(ChromaScale);
        c2 = Vector512.ConvertToSingle(Vector512.WidenLower(chromaB.AsInt16().ToVector512())) * Vector512.Create(ChromaScale);
        c3 = Vector512<float>.Zero;
    }
}
