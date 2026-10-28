// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads packed floating-point RGB or RGBA samples without changing their bit patterns.
/// </summary>
internal readonly struct TiffFloat32Source : ITiffColorSource<float>
{
    private readonly bool reverseEndianness;
    private readonly bool hasAlpha;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFloat32Source"/> struct.
    /// </summary>
    /// <param name="isBigEndian">Whether stored float words use big-endian byte order.</param>
    /// <param name="hasAlpha">Whether the fourth stored component is alpha.</param>
    public TiffFloat32Source(bool isBigEndian, bool hasAlpha)
    {
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.hasAlpha = hasAlpha;
    }

    /// <inheritdoc />
    public int BytesPerPixel => this.hasAlpha ? 16 : 12;

    /// <inheritdoc />
    public int ComponentCount => this.hasAlpha ? 4 : 3;

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
        ReadOnlySpan<uint> samples = MemoryMarshal.Cast<byte, uint>(source0);
        int offset = index * this.ComponentCount;
        uint r = samples[offset];
        uint g = samples[offset + 1];
        uint b = samples[offset + 2];
        uint a = this.hasAlpha ? samples[offset + 3] : BitConverter.SingleToUInt32Bits(1F);

        if (this.reverseEndianness)
        {
            r = BinaryPrimitives.ReverseEndianness(r);
            g = BinaryPrimitives.ReverseEndianness(g);
            b = BinaryPrimitives.ReverseEndianness(b);

            if (this.hasAlpha)
            {
                a = BinaryPrimitives.ReverseEndianness(a);
            }
        }

        c0 = BitConverter.UInt32BitsToSingle(r);
        c1 = BitConverter.UInt32BitsToSingle(g);
        c2 = BitConverter.UInt32BitsToSingle(b);
        c3 = BitConverter.UInt32BitsToSingle(a);
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
        // Reuse the packed-word loaders without rounding, scaling, or clipping the stored float samples.
        if (this.hasAlpha)
        {
            SimdUtils.Deinterleave4(MemoryMarshal.Cast<byte, Vector4>(source0), index, out c0, out c1, out c2, out c3);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out c0, out c1, out c2);
            c3 = Vector128.Create(1F);
        }

        if (this.reverseEndianness)
        {
            // Reverse the bytes of each stored float, including alpha only when it came from the stream.
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsSingle();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsSingle();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsSingle();

            if (this.hasAlpha)
            {
                c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsSingle();
            }
        }
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
        // Reuse the packed-word loaders without rounding, scaling, or clipping the stored float samples.
        if (this.hasAlpha)
        {
            SimdUtils.Deinterleave4(MemoryMarshal.Cast<byte, Vector4>(source0), index, out c0, out c1, out c2, out c3);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out c0, out c1, out c2);
            c3 = Vector256.Create(1F);
        }

        if (this.reverseEndianness)
        {
            // Reverse the bytes of each stored float, including alpha only when it came from the stream.
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsSingle();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsSingle();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsSingle();

            if (this.hasAlpha)
            {
                c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsSingle();
            }
        }
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
        // Reuse the packed-word loaders without rounding, scaling, or clipping the stored float samples.
        if (this.hasAlpha)
        {
            SimdUtils.Deinterleave4(MemoryMarshal.Cast<byte, Vector4>(source0), index, out c0, out c1, out c2, out c3);
        }
        else
        {
            SimdUtils.Deinterleave3(MemoryMarshal.Cast<byte, Vector3>(source0), index, out c0, out c1, out c2);
            c3 = Vector512.Create(1F);
        }

        if (this.reverseEndianness)
        {
            // Reverse the bytes of each stored float, including alpha only when it came from the stream.
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsSingle();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsSingle();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsSingle();

            if (this.hasAlpha)
            {
                c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsSingle();
            }
        }
    }
}
