// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents a CIE L*a*b* 1976 color.
/// <see href="https://en.wikipedia.org/wiki/Lab_color_space"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieLab : IProfileConnectingSpace<CieLab, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CieLab"/> struct.
    /// </summary>
    /// <param name="l">The lightness dimension.</param>
    /// <param name="a">The a (green - magenta) component.</param>
    /// <param name="b">The b (blue - yellow) component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLab(float l, float a, float b)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.L = l;
        this.A = a;
        this.B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLab"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l, a, b components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLab(Vector3 vector)
    {
        this.L = vector.X;
        this.A = vector.Y;
        this.B = vector.Z;
    }

    /// <summary>
    /// Gets the lightness dimension.
    /// <remarks>A value usually ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
    /// </summary>
    public float L { get; }

    /// <summary>
    /// Gets the a color component.
    /// <remarks>A value usually ranging from -100 to 100. Negative is green, positive magenta.</remarks>
    /// </summary>
    public float A { get; }

    /// <summary>
    /// Gets the b color component.
    /// <remarks>A value usually ranging from -100 to 100. Negative is blue, positive is yellow</remarks>
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Compares two <see cref="CieLab"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieLab"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLab"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CieLab left, CieLab right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieLab"/> objects for inequality
    /// </summary>
    /// <param name="left">The <see cref="CieLab"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLab"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CieLab left, CieLab right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector3 v3 = default;
        v3 += this.AsVector3Unsafe();
        v3 += new Vector3(0, 128F, 128F);
        v3 /= new Vector3(100F, 255F, 255F);
        return new Vector4(v3, 1F);
    }

    /// <inheritdoc/>
    public static CieLab FromScaledVector4(Vector4 source)
    {
        Vector3 v3 = source.AsVector3();
        v3 *= new Vector3(100F, 255, 255);
        v3 -= new Vector3(0, 128F, 128F);
        return new CieLab(v3);
    }

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<CieLab> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CieLab FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        // Conversion algorithm described here:
        // http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Lab.html
        CieXyz whitePoint = options.TargetWhitePoint;
        float wx = whitePoint.X, wy = whitePoint.Y, wz = whitePoint.Z;

        float xr = source.X / wx, yr = source.Y / wy, zr = source.Z / wz;

        const float inv116 = 1 / 116F;

        float fx = xr > CieConstants.Epsilon ? MathF.Pow(xr, 0.3333333F) : ((CieConstants.Kappa * xr) + 16F) * inv116;
        float fy = yr > CieConstants.Epsilon ? MathF.Pow(yr, 0.3333333F) : ((CieConstants.Kappa * yr) + 16F) * inv116;
        float fz = zr > CieConstants.Epsilon ? MathF.Pow(zr, 0.3333333F) : ((CieConstants.Kappa * zr) + 16F) * inv116;

        float l = (116F * fy) - 16F;
        float a = 500F * (fx - fy);
        float b = 200F * (fy - fz);

        return new CieLab(l, a, b);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieXyz, Vector3>(source);
        CieXyz white = options.TargetWhitePoint;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector512Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieLab, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector256Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieLab, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector128Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                FromProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieLab, float>(ref destination[i]));
            }
        }

        // Fewer than four colors, or all colors without SIMD, use the single-color contract.
        for (; i < source.Length; i++)
        {
            CieXyz value = source[i];
            destination[i] = FromProfileConnectingSpace(options, in value);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieXyz ToProfileConnectingSpace(ColorConversionOptions options)
    {
        // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Lab_to_XYZ.html
        float l = this.L, a = this.A, b = this.B;
        float fy = (l + 16) / 116F;
        float fx = (a / 500F) + fy;
        float fz = fy - (b / 200F);

        float fx3 = Numerics.Pow3(fx);
        float fz3 = Numerics.Pow3(fz);

        float xr = fx3 > CieConstants.Epsilon ? fx3 : ((116F * fx) - 16F) / CieConstants.Kappa;
        float yr = l > CieConstants.Kappa * CieConstants.Epsilon ? Numerics.Pow3((l + 16F) / 116F) : l / CieConstants.Kappa;
        float zr = fz3 > CieConstants.Epsilon ? fz3 : ((116F * fz) - 16F) / CieConstants.Kappa;

        CieXyz whitePoint = options.SourceWhitePoint;
        Vector3 wxyz = new(whitePoint.X, whitePoint.Y, whitePoint.Z);
        Vector3 xyzr = new(xr, yr, zr);

        return new CieXyz(xyzr * wxyz);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLab> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieLab, Vector3>(source);
        CieXyz white = options.SourceWhitePoint;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector512Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                ToProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector256Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                ToProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector128Count<float>(source.Length - i);
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                ToProfileConnectionSpace(ref x, ref y, ref z, in white);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        // Fewer than four colors, or all colors without SIMD, use the single-color contract.
        for (; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <summary>
    /// Converts unscaled Lab components to XYZ in place, with one color per lane.
    /// </summary>
    /// <param name="l">The input L* values and output X values.</param>
    /// <param name="a">The input a* values and output Y values.</param>
    /// <param name="b">The input b* values and output Z values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToProfileConnectionSpace(ref Vector128<float> l, ref Vector128<float> a, ref Vector128<float> b, in CieXyz whitePoint)
    {
        // Keep Lab's linear/cubic branch tests before multiplying by the reference white.
        Vector128<float> sixteen = Vector128.Create(16F);
        Vector128<float> oneHundredSixteen = Vector128.Create(116F);
        Vector128<float> kappa = Vector128.Create(CieConstants.Kappa);
        Vector128<float> epsilon = Vector128.Create(CieConstants.Epsilon);
        Vector128<float> fy = (l + sixteen) / oneHundredSixteen;
        Vector128<float> fx = (a / Vector128.Create(500F)) + fy;
        Vector128<float> fz = fy - (b / Vector128.Create(200F));
        Vector128<float> fx3 = (fx * fx) * fx;
        Vector128<float> fz3 = (fz * fz) * fz;
        Vector128<float> x = Vector128.ConditionalSelect(Vector128.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
        Vector128<float> y = Vector128.ConditionalSelect(
            Vector128.GreaterThan(l, Vector128.Create(CieConstants.Kappa * CieConstants.Epsilon)),
            (fy * fy) * fy,
            l / kappa);

        Vector128<float> z = Vector128.ConditionalSelect(Vector128.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
        l = x * Vector128.Create(whitePoint.X);
        a = y * Vector128.Create(whitePoint.Y);
        b = z * Vector128.Create(whitePoint.Z);
    }

    /// <summary>
    /// Converts XYZ components to unscaled Lab in place, with one color per lane.
    /// </summary>
    /// <param name="x">The input X values and output L* values.</param>
    /// <param name="y">The input Y values and output a* values.</param>
    /// <param name="z">The input Z values and output b* values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FromProfileConnectionSpace(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, in CieXyz whitePoint)
    {
        Vector128<float> xr = x / Vector128.Create(whitePoint.X);
        Vector128<float> yr = y / Vector128.Create(whitePoint.Y);
        Vector128<float> zr = z / Vector128.Create(whitePoint.Z);
        Vector128<float> epsilon = Vector128.Create(CieConstants.Epsilon);
        Vector128<float> kappa = Vector128.Create(CieConstants.Kappa);
        Vector128<float> sixteen = Vector128.Create(16F);
        Vector128<float> inv116 = Vector128.Create(1F / 116F);
        Vector128<float> xCubic = Vector128.GreaterThan(xr, epsilon);
        Vector128<float> yCubic = Vector128.GreaterThan(yr, epsilon);
        Vector128<float> zCubic = Vector128.GreaterThan(zr, epsilon);

        // Inactive lanes use one as the power base, keeping negative linear values out of logarithms.
        // The shared float power kernel uses double intermediates. Retain the scalar exponent before subtracting near-equal roots.
        Vector128<float> xBase = Vector128.ConditionalSelect(xCubic, xr, Vector128<float>.One);
        Vector128<float> fx = Numerics.Pow(xBase, 0.3333333F);

        Vector128<float> yBase = Vector128.ConditionalSelect(yCubic, yr, Vector128<float>.One);
        Vector128<float> fy = Numerics.Pow(yBase, 0.3333333F);

        Vector128<float> zBase = Vector128.ConditionalSelect(zCubic, zr, Vector128<float>.One);
        Vector128<float> fz = Numerics.Pow(zBase, 0.3333333F);
        fx = Vector128.ConditionalSelect(xCubic, fx, ((kappa * xr) + sixteen) * inv116);
        fy = Vector128.ConditionalSelect(yCubic, fy, ((kappa * yr) + sixteen) * inv116);
        fz = Vector128.ConditionalSelect(zCubic, fz, ((kappa * zr) + sixteen) * inv116);
        x = (Vector128.Create(116F) * fy) - sixteen;
        y = Vector128.Create(500F) * (fx - fy);
        z = Vector128.Create(200F) * (fy - fz);
    }

    /// <summary>
    /// Converts unscaled Lab components to XYZ in place, with one color per lane.
    /// </summary>
    /// <param name="l">The input L* values and output X values.</param>
    /// <param name="a">The input a* values and output Y values.</param>
    /// <param name="b">The input b* values and output Z values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToProfileConnectionSpace(ref Vector256<float> l, ref Vector256<float> a, ref Vector256<float> b, in CieXyz whitePoint)
    {
        // Keep Lab's linear/cubic branch tests before multiplying by the reference white.
        Vector256<float> sixteen = Vector256.Create(16F);
        Vector256<float> oneHundredSixteen = Vector256.Create(116F);
        Vector256<float> kappa = Vector256.Create(CieConstants.Kappa);
        Vector256<float> epsilon = Vector256.Create(CieConstants.Epsilon);
        Vector256<float> fy = (l + sixteen) / oneHundredSixteen;
        Vector256<float> fx = (a / Vector256.Create(500F)) + fy;
        Vector256<float> fz = fy - (b / Vector256.Create(200F));
        Vector256<float> fx3 = (fx * fx) * fx;
        Vector256<float> fz3 = (fz * fz) * fz;
        Vector256<float> x = Vector256.ConditionalSelect(Vector256.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
        Vector256<float> y = Vector256.ConditionalSelect(
            Vector256.GreaterThan(l, Vector256.Create(CieConstants.Kappa * CieConstants.Epsilon)),
            (fy * fy) * fy,
            l / kappa);

        Vector256<float> z = Vector256.ConditionalSelect(Vector256.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
        l = x * Vector256.Create(whitePoint.X);
        a = y * Vector256.Create(whitePoint.Y);
        b = z * Vector256.Create(whitePoint.Z);
    }

    /// <summary>
    /// Converts XYZ components to unscaled Lab in place, with one color per lane.
    /// </summary>
    /// <param name="x">The input X values and output L* values.</param>
    /// <param name="y">The input Y values and output a* values.</param>
    /// <param name="z">The input Z values and output b* values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FromProfileConnectionSpace(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, in CieXyz whitePoint)
    {
        Vector256<float> xr = x / Vector256.Create(whitePoint.X);
        Vector256<float> yr = y / Vector256.Create(whitePoint.Y);
        Vector256<float> zr = z / Vector256.Create(whitePoint.Z);
        Vector256<float> epsilon = Vector256.Create(CieConstants.Epsilon);
        Vector256<float> kappa = Vector256.Create(CieConstants.Kappa);
        Vector256<float> sixteen = Vector256.Create(16F);
        Vector256<float> inv116 = Vector256.Create(1F / 116F);
        Vector256<float> xCubic = Vector256.GreaterThan(xr, epsilon);
        Vector256<float> yCubic = Vector256.GreaterThan(yr, epsilon);
        Vector256<float> zCubic = Vector256.GreaterThan(zr, epsilon);

        // Inactive lanes use one as the power base, keeping negative linear values out of logarithms.
        // The shared float power kernel uses double intermediates. Retain the scalar exponent before subtracting near-equal roots.
        Vector256<float> xBase = Vector256.ConditionalSelect(xCubic, xr, Vector256<float>.One);
        Vector256<float> fx = Numerics.Pow(xBase, 0.3333333F);

        Vector256<float> yBase = Vector256.ConditionalSelect(yCubic, yr, Vector256<float>.One);
        Vector256<float> fy = Numerics.Pow(yBase, 0.3333333F);

        Vector256<float> zBase = Vector256.ConditionalSelect(zCubic, zr, Vector256<float>.One);
        Vector256<float> fz = Numerics.Pow(zBase, 0.3333333F);
        fx = Vector256.ConditionalSelect(xCubic, fx, ((kappa * xr) + sixteen) * inv116);
        fy = Vector256.ConditionalSelect(yCubic, fy, ((kappa * yr) + sixteen) * inv116);
        fz = Vector256.ConditionalSelect(zCubic, fz, ((kappa * zr) + sixteen) * inv116);
        x = (Vector256.Create(116F) * fy) - sixteen;
        y = Vector256.Create(500F) * (fx - fy);
        z = Vector256.Create(200F) * (fy - fz);
    }

    /// <summary>
    /// Converts unscaled Lab components to XYZ in place, with one color per lane.
    /// </summary>
    /// <param name="l">The input L* values and output X values.</param>
    /// <param name="a">The input a* values and output Y values.</param>
    /// <param name="b">The input b* values and output Z values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToProfileConnectionSpace(ref Vector512<float> l, ref Vector512<float> a, ref Vector512<float> b, in CieXyz whitePoint)
    {
        // Keep Lab's linear/cubic branch tests before multiplying by the reference white.
        Vector512<float> sixteen = Vector512.Create(16F);
        Vector512<float> oneHundredSixteen = Vector512.Create(116F);
        Vector512<float> kappa = Vector512.Create(CieConstants.Kappa);
        Vector512<float> epsilon = Vector512.Create(CieConstants.Epsilon);
        Vector512<float> fy = (l + sixteen) / oneHundredSixteen;
        Vector512<float> fx = (a / Vector512.Create(500F)) + fy;
        Vector512<float> fz = fy - (b / Vector512.Create(200F));
        Vector512<float> fx3 = (fx * fx) * fx;
        Vector512<float> fz3 = (fz * fz) * fz;
        Vector512<float> x = Vector512.ConditionalSelect(Vector512.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
        Vector512<float> y = Vector512.ConditionalSelect(
            Vector512.GreaterThan(l, Vector512.Create(CieConstants.Kappa * CieConstants.Epsilon)),
            (fy * fy) * fy,
            l / kappa);

        Vector512<float> z = Vector512.ConditionalSelect(Vector512.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
        l = x * Vector512.Create(whitePoint.X);
        a = y * Vector512.Create(whitePoint.Y);
        b = z * Vector512.Create(whitePoint.Z);
    }

    /// <summary>
    /// Converts XYZ components to unscaled Lab in place, with one color per lane.
    /// </summary>
    /// <param name="x">The input X values and output L* values.</param>
    /// <param name="y">The input Y values and output a* values.</param>
    /// <param name="z">The input Z values and output b* values.</param>
    /// <param name="whitePoint">The reference white for the Lab values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FromProfileConnectionSpace(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, in CieXyz whitePoint)
    {
        Vector512<float> xr = x / Vector512.Create(whitePoint.X);
        Vector512<float> yr = y / Vector512.Create(whitePoint.Y);
        Vector512<float> zr = z / Vector512.Create(whitePoint.Z);
        Vector512<float> epsilon = Vector512.Create(CieConstants.Epsilon);
        Vector512<float> kappa = Vector512.Create(CieConstants.Kappa);
        Vector512<float> sixteen = Vector512.Create(16F);
        Vector512<float> inv116 = Vector512.Create(1F / 116F);
        Vector512<float> xCubic = Vector512.GreaterThan(xr, epsilon);
        Vector512<float> yCubic = Vector512.GreaterThan(yr, epsilon);
        Vector512<float> zCubic = Vector512.GreaterThan(zr, epsilon);

        // Inactive lanes use one as the power base, keeping negative linear values out of logarithms.
        // The shared float power kernel uses double intermediates. Retain the scalar exponent before subtracting near-equal roots.
        Vector512<float> xBase = Vector512.ConditionalSelect(xCubic, xr, Vector512<float>.One);
        Vector512<float> fx = Numerics.Pow(xBase, 0.3333333F);

        Vector512<float> yBase = Vector512.ConditionalSelect(yCubic, yr, Vector512<float>.One);
        Vector512<float> fy = Numerics.Pow(yBase, 0.3333333F);

        Vector512<float> zBase = Vector512.ConditionalSelect(zCubic, zr, Vector512<float>.One);
        Vector512<float> fz = Numerics.Pow(zBase, 0.3333333F);
        fx = Vector512.ConditionalSelect(xCubic, fx, ((kappa * xr) + sixteen) * inv116);
        fy = Vector512.ConditionalSelect(yCubic, fy, ((kappa * yr) + sixteen) * inv116);
        fz = Vector512.ConditionalSelect(zCubic, fz, ((kappa * zr) + sixteen) * inv116);
        x = (Vector512.Create(116F) * fy) - sixteen;
        y = Vector512.Create(500F) * (fx - fy);
        z = Vector512.Create(200F) * (fy - fz);
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.L, this.A, this.B);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"CieLab({this.L:#0.##}, {this.A:#0.##}, {this.B:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CieLab other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieLab other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieLab, Vector3>(ref Unsafe.AsRef(in this));
}
