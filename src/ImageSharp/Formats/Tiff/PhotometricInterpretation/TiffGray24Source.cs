// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads 24-bit grayscale TIFF codes, complementing white-is-zero before float conversion.
/// </summary>
internal readonly struct TiffGray24Source : ITiffColorSource<float>
{
    private readonly bool isBigEndian;
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGray24Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether each stored code uses big-endian byte order.</param>
    /// <param name="whiteIsZero">Whether each integer code is complemented before float conversion.</param>
    public TiffGray24Source(bool isBigEndian, bool whiteIsZero)
    {
        this.isBigEndian = isBigEndian;
        this.whiteIsZero = whiteIsZero;
    }

    /// <inheritdoc />
    public int BytesPerPixel => 3;

    /// <inheritdoc />
    public int ComponentCount => 1;

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out float c0, out float c1, out float c2, out float c3)
    {
        ReadOnlySpan<byte> sample = source0.Slice(index * 3);
        uint value = this.isBigEndian ? TiffUtilities.ConvertToUInt24BigEndian(sample) : TiffUtilities.ConvertToUInt24LittleEndian(sample);
        value = this.whiteIsZero ? 0xFFFFFFU - value : value;
        c0 = value;
        c1 = c2 = c3 = 0;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        Vector128<uint> values = TiffUInt24Samples.ReadFour(source0.Slice(index * 3), this.isBigEndian);
        values = this.whiteIsZero ? Vector128.Create(0xFFFFFFU) - values : values;
        c0 = Vector128.ConvertToSingle(values);
        c1 = c2 = c3 = Vector128<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector128<float> v0, out _, out _, out _);
        this.Read(source0, source1, source2, source3, index + 4, out Vector128<float> v1, out _, out _, out _);
        c0 = Vector256.Create(v0, v1);
        c1 = c2 = c3 = Vector256<float>.Zero;
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector256<float> v0, out _, out _, out _);
        this.Read(source0, source1, source2, source3, index + 8, out Vector256<float> v1, out _, out _, out _);
        c0 = Vector512.Create(v0, v1);
        c1 = c2 = c3 = Vector512<float>.Zero;
    }
}
