// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four-byte grayscale codes, complementing white-is-zero in the integer domain.
/// </summary>
internal readonly struct TiffGray32Source : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGray32Source"/> struct for the stored byte order and grayscale interpretation.
    /// </summary>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    /// <param name="whiteIsZero">Whether the integer code is complemented before conversion to float.</param>
    public TiffGray32Source(bool isBigEndian, bool whiteIsZero)
    {
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.whiteIsZero = whiteIsZero;
    }

    /// <inheritdoc />
    public int BytesPerPixel => 4;

    /// <inheritdoc />
    public int ComponentCount => 1;

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
        uint value = MemoryMarshal.Read<uint>(source0.Slice(index * 4));
        value = this.reverseEndianness ? BinaryPrimitives.ReverseEndianness(value) : value;
        value = this.whiteIsZero ? ~value : value;
        c0 = value;
        c1 = c2 = c3 = 0;
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
        Vector128<uint> values = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        values = this.reverseEndianness ? WZYXShuffle4.Invoke(values.AsByte()).AsUInt32() : values;
        values = this.whiteIsZero ? ~values : values;
        c0 = Vector128.ConvertToSingle(values);
        c1 = c2 = c3 = Vector128<float>.Zero;
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
        Vector256<uint> values = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        values = this.reverseEndianness ? WZYXShuffle4.Invoke(values.AsByte()).AsUInt32() : values;
        values = this.whiteIsZero ? ~values : values;
        c0 = Vector256.ConvertToSingle(values);
        c1 = c2 = c3 = Vector256<float>.Zero;
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
        Vector512<uint> values = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        values = this.reverseEndianness ? WZYXShuffle4.Invoke(values.AsByte()).AsUInt32() : values;
        values = this.whiteIsZero ? ~values : values;
        c0 = Vector512.ConvertToSingle(values);
        c1 = c2 = c3 = Vector512<float>.Zero;
    }
}
