// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Reads four consecutive packed RGBA32 word groups without a channel transpose.
/// </summary>
internal readonly struct TiffRgba32PackedSource : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba32PackedSource"/> struct.
    /// </summary>
    /// <param name="reverseEndianness">Whether stored uint words differ from host byte order.</param>
    public TiffRgba32PackedSource(bool reverseEndianness) => this.reverseEndianness = reverseEndianness;

    /// <inheritdoc/>
    public int BytesPerPixel => 16;

    /// <inheritdoc/>
    public int ComponentCount => 4;

    /// <inheritdoc/>
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out float c0, out float c1, out float c2, out float c3)
    {
        ReadOnlySpan<byte> pixel = source0[(index * 16)..];
        uint red = MemoryMarshal.Read<uint>(pixel);
        uint green = MemoryMarshal.Read<uint>(pixel[4..]);
        uint blue = MemoryMarshal.Read<uint>(pixel[8..]);
        uint alpha = MemoryMarshal.Read<uint>(pixel[12..]);

        if (this.reverseEndianness)
        {
            red = BinaryPrimitives.ReverseEndianness(red);
            green = BinaryPrimitives.ReverseEndianness(green);
            blue = BinaryPrimitives.ReverseEndianness(blue);
            alpha = BinaryPrimitives.ReverseEndianness(alpha);
        }

        c0 = red;
        c1 = green;
        c2 = blue;
        c3 = alpha;
    }

    /// <inheritdoc/>
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        ref uint first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0));
        nuint offset = (uint)index * 4;
        Vector128<uint> p0 = Vector128.LoadUnsafe(ref first, offset);
        Vector128<uint> p1 = Vector128.LoadUnsafe(ref first, offset + 4);
        Vector128<uint> p2 = Vector128.LoadUnsafe(ref first, offset + 8);
        Vector128<uint> p3 = Vector128.LoadUnsafe(ref first, offset + 12);

        if (this.reverseEndianness)
        {
            p0 = WZYXShuffle4.Invoke(p0.AsByte()).AsUInt32();
            p1 = WZYXShuffle4.Invoke(p1.AsByte()).AsUInt32();
            p2 = WZYXShuffle4.Invoke(p2.AsByte()).AsUInt32();
            p3 = WZYXShuffle4.Invoke(p3.AsByte()).AsUInt32();
        }

        c0 = Vector128.ConvertToSingle(p0);
        c1 = Vector128.ConvertToSingle(p1);
        c2 = Vector128.ConvertToSingle(p2);
        c3 = Vector128.ConvertToSingle(p3);
    }

    /// <inheritdoc/>
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
    {
        ref uint first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0));
        nuint offset = (uint)index * 4;
        Vector256<uint> p0 = Vector256.LoadUnsafe(ref first, offset);
        Vector256<uint> p1 = Vector256.LoadUnsafe(ref first, offset + 8);
        Vector256<uint> p2 = Vector256.LoadUnsafe(ref first, offset + 16);
        Vector256<uint> p3 = Vector256.LoadUnsafe(ref first, offset + 24);

        if (this.reverseEndianness)
        {
            p0 = WZYXShuffle4.Invoke(p0.AsByte()).AsUInt32();
            p1 = WZYXShuffle4.Invoke(p1.AsByte()).AsUInt32();
            p2 = WZYXShuffle4.Invoke(p2.AsByte()).AsUInt32();
            p3 = WZYXShuffle4.Invoke(p3.AsByte()).AsUInt32();
        }

        c0 = Vector256.ConvertToSingle(p0);
        c1 = Vector256.ConvertToSingle(p1);
        c2 = Vector256.ConvertToSingle(p2);
        c3 = Vector256.ConvertToSingle(p3);
    }

    /// <inheritdoc/>
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
    {
        ref uint first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0));
        nuint offset = (uint)index * 4;
        Vector512<uint> p0 = Vector512.LoadUnsafe(ref first, offset);
        Vector512<uint> p1 = Vector512.LoadUnsafe(ref first, offset + 16);
        Vector512<uint> p2 = Vector512.LoadUnsafe(ref first, offset + 32);
        Vector512<uint> p3 = Vector512.LoadUnsafe(ref first, offset + 48);

        if (this.reverseEndianness)
        {
            p0 = WZYXShuffle4.Invoke(p0.AsByte()).AsUInt32();
            p1 = WZYXShuffle4.Invoke(p1.AsByte()).AsUInt32();
            p2 = WZYXShuffle4.Invoke(p2.AsByte()).AsUInt32();
            p3 = WZYXShuffle4.Invoke(p3.AsByte()).AsUInt32();
        }

        c0 = Vector512.ConvertToSingle(p0);
        c1 = Vector512.ConvertToSingle(p1);
        c2 = Vector512.ConvertToSingle(p2);
        c3 = Vector512.ConvertToSingle(p3);
    }
}
