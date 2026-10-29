// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Common.Helpers;

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
    /// Copies RGB into a <see cref="Vector4"/> with W set to one.
    /// Retains the RGB values, including values outside [0,1].
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
            Vector512<float> ones = Vector512.Create(1F);

            // BlendVariable selects from 'ones' where the sign-bit of mask lane is set.
            // Using -0f sets only the sign bit, producing an efficient "select lane" mask.
            Vector512<float> alphaSelect = Vector512.Create(0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F, 0F, 0F, 0F, -0F);

            // Three registers contain sixteen RGB triples. Four output registers contain the same colors with W = 1.
            // Indices 16..31 select the second input register. The first and last outputs select W from 'ones'.
            Vector512<int> expandFirst = Vector512.Create(0, 1, 2, 16, 3, 4, 5, 16, 6, 7, 8, 16, 9, 10, 11, 16);
            Vector512<int> expandSecond = Vector512.Create(12, 13, 14, 0, 15, 16, 17, 0, 18, 19, 20, 0, 21, 22, 23, 0);
            Vector512<int> expandThird = Vector512.Create(8, 9, 10, 0, 11, 12, 13, 0, 14, 15, 16, 0, 17, 18, 19, 0);
            Vector512<int> expandLast = Vector512.Create(4, 5, 6, 16, 7, 8, 9, 16, 10, 11, 12, 16, 13, 14, 15, 16);
            nuint count = length.Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Read exactly 48 floats before writing the 64-float expansion. RGB values need no arithmetic or clipping.
                Vector512<int> rgb0 = Vector512.LoadUnsafe(ref src).AsInt32();
                Vector512<int> rgb1 = Vector512.LoadUnsafe(ref src, 16U).AsInt32();
                Vector512<int> rgb2 = Vector512.LoadUnsafe(ref src, 32U).AsInt32();
                Vector512<float> rgba0 = Vector512_.PermuteVar16x32x2(rgb0, expandFirst, ones.AsInt32()).AsSingle();
                Vector512<float> rgba1 = Vector512_.PermuteVar16x32x2(rgb0, expandSecond, rgb1).AsSingle();
                Vector512<float> rgba2 = Vector512_.PermuteVar16x32x2(rgb1, expandThird, rgb2).AsSingle();
                Vector512<float> rgba3 = Vector512_.PermuteVar16x32x2(rgb2, expandLast, ones.AsInt32()).AsSingle();

                // The middle outputs span two RGB inputs, so insert their opaque fourth components after selection.
                rgba0.StoreUnsafe(ref dst);
                Avx512F.BlendVariable(rgba1, ones, alphaSelect).StoreUnsafe(ref dst, 16U);
                Avx512F.BlendVariable(rgba2, ones, alphaSelect).StoreUnsafe(ref dst, 32U);
                rgba3.StoreUnsafe(ref dst, 48U);
                src = ref Unsafe.Add(ref src, 48U);
                dst = ref Unsafe.Add(ref dst, 64U);
            }
        }

        if (Avx2.IsSupported)
        {
            Vector256<float> ones = Vector256.Create(1F);
            Vector256<int> expandEnds = Vector256.Create(0, 1, 2, 0, 3, 4, 5, 0);
            Vector256<int> expandLast = Vector256.Create(2, 3, 4, 0, 5, 6, 7, 0);
            Vector256<int> expandSecond = Vector256.Create(6, 7, 8, 0, 9, 10, 11, 0);
            Vector256<int> expandThird = Vector256.Create(4, 5, 6, 0, 7, 8, 9, 0);

            // Each output contains two [R G B W] colors. Insert one in lanes 3 and 7 after selecting RGB.
            // Eight colors use three complete input registers and four output registers, with no overlapping reads.
            const byte alphaMask = 0x88;
            nuint count = (length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<int> rgb0 = Vector256.LoadUnsafe(ref src).AsInt32();
                Vector256<int> rgb1 = Vector256.LoadUnsafe(ref src, 8U).AsInt32();
                Vector256<int> rgb2 = Vector256.LoadUnsafe(ref src, 16U).AsInt32();

                // The middle outputs join adjacent registers. Indices 8..15 select the second input.
                Vector256<float> rgba0 = Avx2.PermuteVar8x32(rgb0, expandEnds).AsSingle();
                Vector256<float> rgba1 = Vector256_.PermuteVar8x32x2(rgb0, expandSecond, rgb1).AsSingle();
                Vector256<float> rgba2 = Vector256_.PermuteVar8x32x2(rgb1, expandThird, rgb2).AsSingle();
                Vector256<float> rgba3 = Avx2.PermuteVar8x32(rgb2, expandLast).AsSingle();
                Avx.Blend(rgba0, ones, alphaMask).StoreUnsafe(ref dst);
                Avx.Blend(rgba1, ones, alphaMask).StoreUnsafe(ref dst, 8U);
                Avx.Blend(rgba2, ones, alphaMask).StoreUnsafe(ref dst, 16U);
                Avx.Blend(rgba3, ones, alphaMask).StoreUnsafe(ref dst, 24U);
                src = ref Unsafe.Add(ref src, 24U);
                dst = ref Unsafe.Add(ref dst, 32U);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> rgbMask = Vector128.Create(-1, -1, -1, 0);
            Vector128<float> ones = Vector128.Create(1F);
            nuint count = (length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                // Three registers hold [R0 G0 B0 R1], [G1 B1 R2 G2], and [B2 R3 G3 B3].
                // Align adjacent registers to put each RGB triple in lanes 0..2. Lane 3 is replaced with one.
                // These byte operations preserve every RGB bit, including NaN payloads and signed zero.
                Vector128<byte> rgb0 = Vector128.LoadUnsafe(ref src).AsByte();
                Vector128<byte> rgb1 = Vector128.LoadUnsafe(ref src, 4U).AsByte();
                Vector128<byte> rgb2 = Vector128.LoadUnsafe(ref src, 8U).AsByte();
                Vector128<float> rgba1 = Vector128_.AlignRight(rgb1, rgb0, 3 * sizeof(float)).AsSingle();
                Vector128<float> rgba2 = Vector128_.AlignRight(rgb2, rgb1, 2 * sizeof(float)).AsSingle();
                Vector128<float> rgba3 = Vector128_.ShiftRightBytesInVector(rgb2, sizeof(float)).AsSingle();
                Vector128.ConditionalSelect(rgbMask.AsSingle(), rgb0.AsSingle(), ones).StoreUnsafe(ref dst);
                Vector128.ConditionalSelect(rgbMask.AsSingle(), rgba1, ones).StoreUnsafe(ref dst, 4U);
                Vector128.ConditionalSelect(rgbMask.AsSingle(), rgba2, ones).StoreUnsafe(ref dst, 8U);
                Vector128.ConditionalSelect(rgbMask.AsSingle(), rgba3, ones).StoreUnsafe(ref dst, 12U);
                src = ref Unsafe.Add(ref src, 12U);
                dst = ref Unsafe.Add(ref dst, 16U);
            }
        }

        // Fewer than four colors remain on SIMD hardware. Without SIMD, this handles the complete span.
        for (; i < length; i++)
        {
            Unsafe.Add(ref dstV4, (uint)i) = Unsafe.Add(ref srcRgb, (uint)i).ToScaledVector4();
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
            // Four registers contain sixteen [R G B W] colors. Select RGB into three complete output registers.
            // Each mask joins two neighboring input registers; indices 16..31 select the second register.
            Vector512<int> packFirst = Vector512.Create(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 16, 17, 18, 20);
            Vector512<int> packMiddle = Vector512.Create(5, 6, 8, 9, 10, 12, 13, 14, 16, 17, 18, 20, 21, 22, 24, 25);
            Vector512<int> packLast = Vector512.Create(10, 12, 13, 14, 16, 17, 18, 20, 21, 22, 24, 25, 26, 28, 29, 30);
            nuint count = length.Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Load all sixteen colors before writing exactly 48 RGB floats. No store crosses the converted prefix.
                Vector512<int> rgba0 = Vector512.LoadUnsafe(ref src).AsInt32();
                Vector512<int> rgba1 = Vector512.LoadUnsafe(ref src, 16U).AsInt32();
                Vector512<int> rgba2 = Vector512.LoadUnsafe(ref src, 32U).AsInt32();
                Vector512<int> rgba3 = Vector512.LoadUnsafe(ref src, 48U).AsInt32();
                Vector512_.PermuteVar16x32x2(rgba0, packFirst, rgba1).AsSingle().StoreUnsafe(ref dst);
                Vector512_.PermuteVar16x32x2(rgba1, packMiddle, rgba2).AsSingle().StoreUnsafe(ref dst, 16U);
                Vector512_.PermuteVar16x32x2(rgba2, packLast, rgba3).AsSingle().StoreUnsafe(ref dst, 32U);
                src = ref Unsafe.Add(ref src, 64U);
                dst = ref Unsafe.Add(ref dst, 48U);
            }
        }

        if (Avx2.IsSupported)
        {
            Vector256<int> packFirst = Vector256.Create(0, 1, 2, 4, 5, 6, 8, 9);
            Vector256<int> packMiddle = Vector256.Create(2, 4, 5, 6, 8, 9, 10, 12);
            Vector256<int> packLast = Vector256.Create(5, 6, 8, 9, 10, 12, 13, 14);
            nuint count = (length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                // Four registers hold eight [R G B W] colors. Join RGB from neighboring registers into three outputs.
                // Read the whole block first and write exactly 24 floats, including when the block ends the span.
                Vector256<int> rgba0 = Vector256.LoadUnsafe(ref src).AsInt32();
                Vector256<int> rgba1 = Vector256.LoadUnsafe(ref src, 8U).AsInt32();
                Vector256<int> rgba2 = Vector256.LoadUnsafe(ref src, 16U).AsInt32();
                Vector256<int> rgba3 = Vector256.LoadUnsafe(ref src, 24U).AsInt32();
                Vector256_.PermuteVar8x32x2(rgba0, packFirst, rgba1).AsSingle().StoreUnsafe(ref dst);
                Vector256_.PermuteVar8x32x2(rgba1, packMiddle, rgba2).AsSingle().StoreUnsafe(ref dst, 8U);
                Vector256_.PermuteVar8x32x2(rgba2, packLast, rgba3).AsSingle().StoreUnsafe(ref dst, 16U);
                src = ref Unsafe.Add(ref src, 32U);
                dst = ref Unsafe.Add(ref dst, 24U);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                // Load all four [R G B W] inputs before storing, so compaction at the same address is safe.
                Vector128<float> rgba0 = Vector128.LoadUnsafe(ref src);
                Vector128<float> rgba1 = Vector128.LoadUnsafe(ref src, 4U);
                Vector128<float> rgba2 = Vector128.LoadUnsafe(ref src, 8U);
                Vector128<float> rgba3 = Vector128.LoadUnsafe(ref src, 12U);

                // Each following store replaces the preceding W with its R. The final store writes only RGB,
                // so all stores stay within these twelve output floats. This needs no shuffles or component arithmetic.
                rgba0.StoreUnsafe(ref dst);
                rgba1.StoreUnsafe(ref dst, 3U);
                rgba2.StoreUnsafe(ref dst, 6U);
                Unsafe.As<float, Vector3>(ref Unsafe.Add(ref dst, 9U)) = rgba3.AsVector3();
                src = ref Unsafe.Add(ref src, 16U);
                dst = ref Unsafe.Add(ref dst, 12U);
            }
        }

        // Fewer than four colors remain on SIMD hardware. Without SIMD, this handles the complete span.
        for (; i < length; i++)
        {
            Vector4 v = Unsafe.Add(ref srcV4, (uint)i);
            Unsafe.Add(ref dstRgb, (uint)i) = FromScaledVector4(v);
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
                    SimdUtils.Transpose4(
                        r,
                        g,
                        b,
                        Vector128.Create(1F),
                        out Vector128<float> pixel0,
                        out Vector128<float> pixel1,
                        out Vector128<float> pixel2,
                        out Vector128<float> pixel3);

                    ref Vector128<float> target = ref Unsafe.As<Vector4, Vector128<float>>(ref block[i]);
                    target = pixel0;
                    Unsafe.Add(ref target, 1U) = pixel1;
                    Unsafe.Add(ref target, 2U) = pixel2;
                    Unsafe.Add(ref target, 3U) = pixel3;
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
                    SimdUtils.Transpose4(
                        first,
                        Unsafe.Add(ref first, 1U),
                        Unsafe.Add(ref first, 2U),
                        Unsafe.Add(ref first, 3U),
                        out Vector128<float> r,
                        out Vector128<float> g,
                        out Vector128<float> b,
                        out _);

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
    /// Copies the RGB components to a <see cref="Vector3"/>, retaining values outside [0,1].
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

    /// <summary>
    /// Reads the three stored components without copying or changing their range.
    /// </summary>
    /// <returns>The RGB components in their stored order.</returns>
    internal Vector3 AsVector3Unsafe() => Unsafe.As<Rgb, Vector3>(ref Unsafe.AsRef(in this));
}
