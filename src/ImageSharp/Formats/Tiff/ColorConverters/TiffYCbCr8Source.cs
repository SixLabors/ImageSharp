// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

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
        c0 = source0[offset];
        c1 = (this.planar ? source1 : source0)[offset + (this.planar ? 0 : 1)];
        c2 = (this.planar ? source2 : source0)[offset + (this.planar ? 0 : 2)];
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
            Vector128<byte> samples = Vector128.Create(
                MemoryMarshal.Read<uint>(packed),
                MemoryMarshal.Read<uint>(packed.Slice(4)),
                MemoryMarshal.Read<uint>(packed.Slice(8)),
                0U).AsByte();

            // The first four indices select channel bytes; the high bit zeros the unused lanes in the native byte shuffle.
            y = Vector128.ShuffleNative(samples, Vector128.Create((byte)0, 3, 6, 9, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
            cb = Vector128.ShuffleNative(samples, Vector128.Create((byte)1, 4, 7, 10, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
            cr = Vector128.ShuffleNative(samples, Vector128.Create((byte)2, 5, 8, 11, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128));
        }

        c0 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(y)).AsUInt32());
        c1 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(cb)).AsUInt32());
        c2 = Vector128.ConvertToSingle(Vector128.WidenLower(Vector128.WidenLower(cr)).AsUInt32());
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

        c0 = Vector256.ConvertToSingle(Vector256_.Widen(first));
        c1 = Vector256.ConvertToSingle(Vector256_.Widen(second));
        c2 = Vector256.ConvertToSingle(Vector256_.Widen(third));
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

        c0 = Vector512.ConvertToSingle(Vector512_.Widen(first));
        c1 = Vector512.ConvertToSingle(Vector512_.Widen(second));
        c2 = Vector512.ConvertToSingle(Vector512_.Widen(third));
        c3 = Vector512<float>.Zero;
    }
}
