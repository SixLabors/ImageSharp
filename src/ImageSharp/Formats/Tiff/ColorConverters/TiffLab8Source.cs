// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            // The first four indices select channel bytes; the high bit zeros the unused lanes in the native byte shuffle.
            lightness = Vector128.ShuffleNative(samples, Vector128.Create((byte)0, 3, 6, 9, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
            chromaA = Vector128.ShuffleNative(samples, Vector128.Create((byte)1, 4, 7, 10, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
            chromaB = Vector128.ShuffleNative(samples, Vector128.Create((byte)2, 5, 8, 11, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
        }

        // The TIFF chroma byte is two's-complement signed; widen it as sbyte before float conversion.
        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(lightness)).AsUInt32()) * Vector128.Create(100F) * Vector128.Create(Inv255);
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(chromaA.AsSByte())).AsInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(chromaB.AsSByte())).AsInt32());
        c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        Vector64<byte> first;
        Vector64<byte> second;
        Vector64<byte> third;

        if (this.planar)
        {
            // Eight byte codes widen directly to eight float lanes without loading the following group.
            first = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(source0), (nuint)index);
            second = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(source1), (nuint)index);
            third = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(source2), (nuint)index);
        }
        else
        {
            SimdUtils.Deinterleave3(source0, index, out first, out second, out third);
        }

        // Preserve signed chroma and the established lightness multiplication order.
        c0 = Vector256.ConvertToSingle(Vector256_.Widen(first)) * Vector256.Create(100F) * Vector256.Create(Inv255);
        c1 = Vector256.ConvertToSingle(Vector256_.Widen(second.AsSByte()));
        c2 = Vector256.ConvertToSingle(Vector256_.Widen(third.AsSByte()));
        c3 = Vector256<float>.Zero;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        Vector128<byte> first;
        Vector128<byte> second;
        Vector128<byte> third;

        if (this.planar)
        {
            // Sixteen byte codes become sixteen float lanes. Each complete plane load is exactly one 128-bit register.
            first = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source0), (nuint)index);
            second = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source1), (nuint)index);
            third = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source2), (nuint)index);
        }
        else
        {
            // Load the complete sixteen-pixel group once, rather than assembling four separate four-pixel conversions.
            SimdUtils.Deinterleave3(source0, index, out first, out second, out third);
        }

        // Lab lightness is unsigned; chroma bytes are signed. Preserve the established scaling order.
        c0 = Vector512.ConvertToSingle(Vector512_.Widen(first)) * Vector512.Create(100F) * Vector512.Create(Inv255);
        c1 = Vector512.ConvertToSingle(Vector512_.Widen(second.AsSByte()));
        c2 = Vector512.ConvertToSingle(Vector512_.Widen(third.AsSByte()));
        c3 = Vector512<float>.Zero;
    }
}
