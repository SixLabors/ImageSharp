// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads one floating-point TIFF grayscale word without changing its numeric bits.
/// </summary>
internal readonly struct TiffGrayFloat32Source : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayFloat32Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    public TiffGrayFloat32Source(bool isBigEndian) => this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;

    /// <inheritdoc />
    public int BytesPerPixel => sizeof(float);

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
        uint bits = MemoryMarshal.Read<uint>(source0.Slice(index * sizeof(float)));
        c0 = BitConverter.UInt32BitsToSingle(this.reverseEndianness ? BinaryPrimitives.ReverseEndianness(bits) : bits);
        c1 = c2 = c3 = 0F;
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
        Vector128<uint> words = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c0 = this.reverseEndianness ? WZYXShuffle4.Invoke(words.AsByte()).AsSingle() : words.AsSingle();
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
        Vector256<uint> words = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c0 = this.reverseEndianness ? WZYXShuffle4.Invoke(words.AsByte()).AsSingle() : words.AsSingle();
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
        Vector512<uint> words = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0)), (nuint)index);
        c0 = this.reverseEndianness ? WZYXShuffle4.Invoke(words.AsByte()).AsSingle() : words.AsSingle();
        c1 = c2 = c3 = Vector512<float>.Zero;
    }
}
