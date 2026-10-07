// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

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
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out float c0, out float c1, out float c2, out float c3)
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
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
    {
        ref uint first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, uint>(source0));
        ref uint group = ref Unsafe.Add(ref first, index * this.ComponentCount);
        Vector128<uint> pixel0;
        Vector128<uint> pixel1;
        Vector128<uint> pixel2;
        Vector128<uint> pixel3;

        if (this.hasAlpha)
        {
            pixel0 = Unsafe.As<uint, Vector128<uint>>(ref group);
            pixel1 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 4));
            pixel2 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 8));
            pixel3 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 12));
        }
        else
        {
            // The final Vector3 load consumes exactly the last three stored words.
            pixel0 = Unsafe.As<uint, Vector128<uint>>(ref group);
            pixel1 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 3));
            pixel2 = Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref group, 6));
            pixel3 = Unsafe.As<uint, Vector3>(ref Unsafe.Add(ref group, 9)).AsVector128().AsUInt32();
        }

        // Only bitwise transposition moves the stored float words into channel registers.
        SimdUtils.Transpose4(pixel0.AsSingle(), pixel1.AsSingle(), pixel2.AsSingle(), pixel3.AsSingle(), out c0, out c1, out c2, out c3);

        if (this.reverseEndianness)
        {
            c0 = WZYXShuffle4.Invoke(c0.AsByte()).AsSingle();
            c1 = WZYXShuffle4.Invoke(c1.AsByte()).AsSingle();
            c2 = WZYXShuffle4.Invoke(c2.AsByte()).AsSingle();

            if (this.hasAlpha)
            {
                c3 = WZYXShuffle4.Invoke(c3.AsByte()).AsSingle();
            }
        }

        if (!this.hasAlpha)
        {
            c3 = Vector128.Create(1F);
        }
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2, out Vector256<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector128<float> r0, out Vector128<float> g0, out Vector128<float> b0, out Vector128<float> a0);
        this.Read(source0, source1, source2, source3, index + 4, out Vector128<float> r1, out Vector128<float> g1, out Vector128<float> b1, out Vector128<float> a1);
        c0 = Vector256.Create(r0, r1);
        c1 = Vector256.Create(g0, g1);
        c2 = Vector256.Create(b0, b1);
        c3 = Vector256.Create(a0, a1);
    }

    /// <inheritdoc />
    public void Read(ReadOnlySpan<byte> source0, ReadOnlySpan<byte> source1, ReadOnlySpan<byte> source2, ReadOnlySpan<byte> source3, int index, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2, out Vector512<float> c3)
    {
        this.Read(source0, source1, source2, source3, index, out Vector256<float> r0, out Vector256<float> g0, out Vector256<float> b0, out Vector256<float> a0);
        this.Read(source0, source1, source2, source3, index + 8, out Vector256<float> r1, out Vector256<float> g1, out Vector256<float> b1, out Vector256<float> a1);
        c0 = Vector512.Create(r0, r1);
        c1 = Vector512.Create(g0, g1);
        c2 = Vector512.Create(b0, b1);
        c3 = Vector512.Create(a0, a1);
    }
}
