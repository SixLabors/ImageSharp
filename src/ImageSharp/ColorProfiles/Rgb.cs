// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an RGB (red, green, blue) color profile.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Rgb : IProfileConnectingSpace<Rgb, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb"/> struct.
    /// </summary>
    /// <param name="r">The red component usually ranging between 0 and 1.</param>
    /// <param name="g">The green component usually ranging between 0 and 1.</param>
    /// <param name="b">The blue component usually ranging between 0 and 1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgb(float r, float g, float b)
    {
        // Not clamping as this space can exceed "usual" ranges
        this.R = r;
        this.G = g;
        this.B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb"/> struct.
    /// </summary>
    /// <param name="source">The vector representing the r, g, b components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgb(Vector3 source)
    {
        this.R = source.X;
        this.G = source.Y;
        this.B = source.Z;
    }

    /// <summary>
    /// Gets the red component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float R { get; }

    /// <summary>
    /// Gets the green component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float G { get; }

    /// <summary>
    /// Gets the blue component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Compares two <see cref="Rgb"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgb"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rgb left, Rgb right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Rgb"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgb"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rgb left, Rgb right) => !left.Equals(right);

    /// <summary>
    /// Initializes the color instance from a generic scaled <see cref="Vector4"/>.
    /// </summary>
    /// <param name="source">The vector to load the color from.</param>
    /// <returns>The <see cref="Rgb"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb FromScaledVector4(Vector4 source)
        => new(source.AsVector3());

    /// <summary>
    /// Expands the color into a generic ("scaled") <see cref="Vector4"/> representation
    /// with values scaled and usually clamped between <value>0</value> and <value>1</value>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToScaledVector4()
        => new(this.AsVector3Unsafe(), 1F);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Rgb> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        int length = source.Length;
        if (length == 0)
        {
            return;
        }

        ref Rgb srcRgb = ref MemoryMarshal.GetReference(source);
        ref Vector4 dstV4 = ref MemoryMarshal.GetReference(destination);

        // Float streams:
        // src: r0 g0 b0 r1 g1 b1 ...
        // dst: r0 g0 b0 a0 r1 g1 b1 a1 ...
        ref float src = ref Unsafe.As<Rgb, float>(ref srcRgb);
        ref float dst = ref Unsafe.As<Vector4, float>(ref dstV4);

        int i = 0;

        if (Avx512F.IsSupported)
        {
            // 4 pixels per iteration. Using overlapped 16-float loads.
            Vector512<int> perm = Vector512.Create(0, 1, 2, 0, 3, 4, 5, 0, 6, 7, 8, 0, 9, 10, 11, 0);
            Vector512<float> ones = Vector512.Create(1F);

            // BlendVariable selects from 'ones' where the sign-bit of mask lane is set.
            // Using -0f sets only the sign bit, producing an efficient "select lane" mask.
            Vector512<float> alphaSelect = Vector512.Create(0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F);

            int quads = length >> 2;

            // Leave the last quad (4 pixels) for the scalar tail.
            int simdQuads = quads - 1;

            for (int q = 0; q < simdQuads; q++)
            {
                Vector512<float> v = ReadVector512(ref src);
                Vector512<float> rgbx = Avx512F.PermuteVar16x32(v, perm);
                Vector512<float> rgba = Avx512F.BlendVariable(rgbx, ones, alphaSelect);

                WriteVector512(ref dst, rgba);

                src = ref Unsafe.Add(ref src, 12);
                dst = ref Unsafe.Add(ref dst, 16);

                i += 4;
            }
        }
        else if (Avx2.IsSupported)
        {
            // 2 pixels per iteration. Using overlapped 8-float loads.
            Vector256<int> perm = Vector256.Create(0, 1, 2, 0, 3, 4, 5, 0);

            Vector256<float> ones = Vector256.Create(1F);

            // vblendps mask: bit i selects lane i from 'ones' when set.
            // We want lanes 3 and 7 -> 0b10001000 = 0x88.
            const byte alphaMask = 0x88;

            int pairs = length >> 1;

            // Leave the last pair (2 pixels) for the scalar tail.
            int simdPairs = pairs - 1;

            for (int p = 0; p < simdPairs; p++)
            {
                Vector256<float> v = ReadVector256(ref src);
                Vector256<float> rgbx = Avx2.PermuteVar8x32(v, perm);
                Vector256<float> rgba = Avx.Blend(rgbx, ones, alphaMask);

                WriteVector256(ref dst, rgba);

                src = ref Unsafe.Add(ref src, 6);
                dst = ref Unsafe.Add(ref dst, 8);

                i += 2;
            }
        }

        // Tail (and non-AVX paths)
        for (; i < length; i++)
        {
            Unsafe.Add(ref dstV4, i) = Unsafe.Add(ref srcRgb, i).ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        int length = source.Length;
        if (length == 0)
        {
            return;
        }

        ref Vector4 srcV4 = ref MemoryMarshal.GetReference(source);
        ref Rgb dstRgb = ref MemoryMarshal.GetReference(destination);

        // Float streams:
        // src: r0 g0 b0 a0 r1 g1 b1 a1 ...
        // dst: r0 g0 b0 r1 g1 b1 ...
        ref float src = ref Unsafe.As<Vector4, float>(ref srcV4);
        ref float dst = ref Unsafe.As<Rgb, float>(ref dstRgb);

        int i = 0;

        if (Avx512F.IsSupported)
        {
            // 4 pixels per iteration. Using overlapped 16-float stores:
            Vector512<int> idx = Vector512.Create(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 3, 7, 11, 15);

            // Number of 4-pixel groups in the input.
            int quads = length >> 2;

            // Leave the last quad (4 pixels) for the scalar tail.
            int simdQuads = quads - 1;

            for (int q = 0; q < simdQuads; q++)
            {
                Vector512<float> v = ReadVector512(ref src);
                Vector512<float> packed = Avx512F.PermuteVar16x32(v, idx);

                WriteVector512(ref dst, packed);

                src = ref Unsafe.Add(ref src, 16);
                dst = ref Unsafe.Add(ref dst, 12);
                i += 4;
            }
        }
        else if (Avx2.IsSupported)
        {
            // 2 pixels per iteration, using overlapped 8-float stores:
            Vector256<int> idx = Vector256.Create(0, 1, 2, 4, 5, 6, 0, 0);

            int pairs = length >> 1;

            // Leave the last pair (2 pixels) for the scalar tail.
            int simdPairs = pairs - 1;

            int pairIndex = 0;
            for (; pairIndex < simdPairs; pairIndex++)
            {
                Vector256<float> v = ReadVector256(ref src);
                Vector256<float> packed = Avx2.PermuteVar8x32(v, idx);

                WriteVector256(ref dst, packed);

                src = ref Unsafe.Add(ref src, 8);
                dst = ref Unsafe.Add(ref dst, 6);
                i += 2;
            }
        }

        // Tail (and non-AVX paths)
        for (; i < length; i++)
        {
            Vector4 v = Unsafe.Add(ref srcV4, i);
            Unsafe.Add(ref dstRgb, i) = FromScaledVector4(v);
        }
    }

    /// <inheritdoc/>
    public static Rgb FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        // Convert to linear rgb then compress.
        Rgb linear = new(Vector3.Transform(source.AsVector3Unsafe(), options.TargetRgbWorkingSpace.CieXyzToRgbMatrix));
        return FromScaledVector4(options.TargetRgbWorkingSpace.Compress(linear.ToScaledVector4()));
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Matrix4x4 matrix = options.TargetRgbWorkingSpace.CieXyzToRgbMatrix;
        InlineArray16<Vector4> buffer = default;
        Span<Vector4> vectors = buffer;
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieXyz, Vector3>(source);

        for (int offset = 0; offset < source.Length; offset += 16)
        {
            int count = Math.Min(16, source.Length - offset);
            Span<Vector4> block = vectors[..count];
            int i = 0;
            ReadOnlySpan<float> components = MemoryMarshal.Cast<CieXyz, float>(source.Slice(offset, count));

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = components.Vector512Count<float>() / 3;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
                {
                    // Sixteen packed XYZ triples become sixteen linear RGB vectors with opaque fourth lanes.
                    SimdUtils.Deinterleave3(packed, offset + i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                    SimdUtils.Transform3(in matrix, x, y, z, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b);
                    SimdUtils.Interleave4(r, g, b, Vector512.Create(1F), ref Unsafe.As<Vector4, float>(ref block[i]));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = components[(i * 3)..].Vector256Count<float>() / 3;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
                {
                    // The eight-pixel remainder uses the same matrix and leaves alpha opaque for companding.
                    SimdUtils.Deinterleave3(packed, offset + i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                    SimdUtils.Transform3(in matrix, x, y, z, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b);
                    SimdUtils.Interleave4(r, g, b, Vector256.Create(1F), ref Unsafe.As<Vector4, float>(ref block[i]));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = components[(i * 3)..].Vector128Count<float>() / 3;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
                {
                    SimdUtils.Deinterleave3(packed, offset + i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                    SimdUtils.Transform3(in matrix, x, y, z, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b);

                    // Transpose the linear channels into the existing working-space Vector4 span contract.
                    SimdUtils.Transpose4(r, g, b, Vector128.Create(1F), out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);
                    ref Vector128<float> target = ref Unsafe.As<Vector4, Vector128<float>>(ref block[i]);
                    target = pixel0;
                    Unsafe.Add(ref target, 1) = pixel1;
                    Unsafe.Add(ref target, 2) = pixel2;
                    Unsafe.Add(ref target, 3) = pixel3;
                }
            }

            for (; i < count; i++)
            {
                block[i] = new Vector4(Vector3.Transform(source[offset + i].AsVector3Unsafe(), matrix), 1F);
            }

            options.TargetRgbWorkingSpace.Compress(block);
            FromScaledVector4(block, destination.Slice(offset, count));
        }
    }

    /// <inheritdoc/>
    public CieXyz ToProfileConnectingSpace(ColorConversionOptions options)
    {
        // First expand to linear rgb
        Rgb linear = FromScaledVector4(options.SourceRgbWorkingSpace.Expand(this.ToScaledVector4()));

        // Then convert to xyz
        return new CieXyz(Vector3.Transform(linear.AsVector3Unsafe(), options.SourceRgbWorkingSpace.RgbToCieXyzMatrix));
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Matrix4x4 matrix = options.SourceRgbWorkingSpace.RgbToCieXyzMatrix;
        InlineArray16<Vector4> buffer = default;
        Span<Vector4> vectors = buffer;

        for (int offset = 0; offset < source.Length; offset += 16)
        {
            int count = Math.Min(16, source.Length - offset);
            Span<Vector4> block = vectors[..count];

            // The working space owns companding for all supported transfer functions and RGB ranges.
            ToScaledVector4(source.Slice(offset, count), block);
            options.SourceRgbWorkingSpace.Expand(block);
            int i = 0;
            ReadOnlySpan<float> components = MemoryMarshal.Cast<Vector4, float>(block);

            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = components.Vector512Count<float>() / 4;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
                {
                    // The working-space buffer is RGBA; the fourth lane is ignored after full-group deinterleave.
                    SimdUtils.Deinterleave4(block, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b, out _);
                    SimdUtils.Transform3(in matrix, r, g, b, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                    SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[offset + i]));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = components[(i * 4)..].Vector256Count<float>() / 4;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
                {
                    // Eight-pixel groups keep matrix arithmetic wide while storing exactly eight XYZ triples.
                    SimdUtils.Deinterleave4(block, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b, out _);
                    SimdUtils.Transform3(in matrix, r, g, b, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                    SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[offset + i]));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = components[(i * 4)..].Vector128Count<float>() / 4;

                for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
                {
                    ref Vector128<float> first = ref Unsafe.As<Vector4, Vector128<float>>(ref block[i]);
                    SimdUtils.Transpose4(first, Unsafe.Add(ref first, 1), Unsafe.Add(ref first, 2), Unsafe.Add(ref first, 3), out Vector128<float> r, out Vector128<float> g, out Vector128<float> b, out _);
                    SimdUtils.Transform3(in matrix, r, g, b, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                    SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[offset + i]));
                }
            }

            for (; i < count; i++)
            {
                destination[offset + i] = new CieXyz(Vector3.Transform(block[i].AsVector3(), matrix));
            }
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <summary>
    /// Initializes the color instance from a generic scaled <see cref="Vector3"/>.
    /// </summary>
    /// <param name="source">The vector to load the color from.</param>
    /// <returns>The <see cref="Rgb"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb FromScaledVector3(Vector3 source)
        => new(source);

    /// <summary>
    /// Initializes the color instance for a source clamped between <value>0</value> and <value>1</value>
    /// </summary>
    /// <param name="source">The source to load the color from.</param>
    /// <returns>The <see cref="Rgb"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb Clamp(Rgb source)
        => new(Vector3.Clamp(source.AsVector3Unsafe(), Vector3.Zero, Vector3.One));

    /// <summary>
    /// Expands the color into a generic ("scaled") <see cref="Vector3"/> representation
    /// with values scaled and usually clamped between <value>0</value> and <value>1</value>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector3"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ToScaledVector3()
    {
        Vector3 v3 = default;
        v3 += this.AsVector3Unsafe();
        return v3;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.R, this.G, this.B);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Rgb({this.R:#0.##}, {this.G:#0.##}, {this.B:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rgb other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Rgb other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    internal Vector3 AsVector3Unsafe() => Unsafe.As<Rgb, Vector3>(ref Unsafe.AsRef(in this));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ReadVector512(ref float src)
    {
        ref byte b = ref Unsafe.As<float, byte>(ref src);
        return Unsafe.ReadUnaligned<Vector512<float>>(ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ReadVector256(ref float src)
    {
        ref byte b = ref Unsafe.As<float, byte>(ref src);
        return Unsafe.ReadUnaligned<Vector256<float>>(ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteVector512(ref float dst, Vector512<float> value)
    {
        ref byte b = ref Unsafe.As<float, byte>(ref dst);
        Unsafe.WriteUnaligned(ref b, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteVector256(ref float dst, Vector256<float> value)
    {
        ref byte b = ref Unsafe.As<float, byte>(ref dst);
        Unsafe.WriteUnaligned(ref b, value);
    }
}
