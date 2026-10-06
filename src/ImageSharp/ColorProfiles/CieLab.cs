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

        for (int i = 0; i < source.Length; i++)
        {
            CieXyz xyz = source[i];
            destination[i] = FromProfileConnectingSpace(options, in xyz);
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

        int i = 0;
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieLab, Vector3>(source);
        ReadOnlySpan<float> components = MemoryMarshal.Cast<CieLab, float>(source);
        CieXyz whitePoint = options.SourceWhitePoint;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> sixteen = Vector512.Create(16F);
            Vector512<float> oneHundredSixteen = Vector512.Create(116F);
            Vector512<float> epsilon = Vector512.Create(CieConstants.Epsilon);
            Vector512<float> kappa = Vector512.Create(CieConstants.Kappa);
            Vector512<float> lightnessThreshold = Vector512.Create(CieConstants.Kappa * CieConstants.Epsilon);

            // Three packed component registers contain sixteen complete Lab pixels.
            nuint vectorCount = components.Vector512Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> l, out Vector512<float> a, out Vector512<float> b);

                // Retain Lab's linear/cubic branch tests before the configured source-white scaling.
                Vector512<float> fy = (l + sixteen) / oneHundredSixteen;
                Vector512<float> fx = (a / Vector512.Create(500F)) + fy;
                Vector512<float> fz = fy - (b / Vector512.Create(200F));
                Vector512<float> fx3 = (fx * fx) * fx;
                Vector512<float> fz3 = (fz * fz) * fz;
                Vector512<float> xr = Vector512.ConditionalSelect(Vector512.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
                Vector512<float> yr = Vector512.ConditionalSelect(Vector512.GreaterThan(l, lightnessThreshold), (fy * fy) * fy, l / kappa);
                Vector512<float> zr = Vector512.ConditionalSelect(Vector512.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
                Vector512<float> x = xr * Vector512.Create(whitePoint.X);
                Vector512<float> y = yr * Vector512.Create(whitePoint.Y);
                Vector512<float> z = zr * Vector512.Create(whitePoint.Z);

                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> sixteen = Vector256.Create(16F);
            Vector256<float> oneHundredSixteen = Vector256.Create(116F);
            Vector256<float> epsilon = Vector256.Create(CieConstants.Epsilon);
            Vector256<float> kappa = Vector256.Create(CieConstants.Kappa);
            Vector256<float> lightnessThreshold = Vector256.Create(CieConstants.Kappa * CieConstants.Epsilon);

            // Three packed component registers contain eight complete Lab pixels.
            nuint vectorCount = components[(i * 3)..].Vector256Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> l, out Vector256<float> a, out Vector256<float> b);

                Vector256<float> fy = (l + sixteen) / oneHundredSixteen;
                Vector256<float> fx = (a / Vector256.Create(500F)) + fy;
                Vector256<float> fz = fy - (b / Vector256.Create(200F));
                Vector256<float> fx3 = (fx * fx) * fx;
                Vector256<float> fz3 = (fz * fz) * fz;
                Vector256<float> xr = Vector256.ConditionalSelect(Vector256.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
                Vector256<float> yr = Vector256.ConditionalSelect(Vector256.GreaterThan(l, lightnessThreshold), (fy * fy) * fy, l / kappa);
                Vector256<float> zr = Vector256.ConditionalSelect(Vector256.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
                Vector256<float> x = xr * Vector256.Create(whitePoint.X);
                Vector256<float> y = yr * Vector256.Create(whitePoint.Y);
                Vector256<float> z = zr * Vector256.Create(whitePoint.Z);

                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> sixteen = Vector128.Create(16F);
            Vector128<float> oneHundredSixteen = Vector128.Create(116F);
            Vector128<float> epsilon = Vector128.Create(CieConstants.Epsilon);
            Vector128<float> kappa = Vector128.Create(CieConstants.Kappa);
            Vector128<float> lightnessThreshold = Vector128.Create(CieConstants.Kappa * CieConstants.Epsilon);

            // Three packed component registers contain four complete Lab pixels.
            nuint vectorCount = components[(i * 3)..].Vector128Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> l, out Vector128<float> a, out Vector128<float> b);

                // Keep the scalar Lab branch tests in their original domains; negative and HDR coordinates remain valid.
                Vector128<float> fy = (l + sixteen) / oneHundredSixteen;
                Vector128<float> fx = (a / Vector128.Create(500F)) + fy;
                Vector128<float> fz = fy - (b / Vector128.Create(200F));
                Vector128<float> fx3 = (fx * fx) * fx;
                Vector128<float> fz3 = (fz * fz) * fz;
                Vector128<float> xr = Vector128.ConditionalSelect(Vector128.GreaterThan(fx3, epsilon), fx3, ((oneHundredSixteen * fx) - sixteen) / kappa);
                Vector128<float> yr = Vector128.ConditionalSelect(Vector128.GreaterThan(l, lightnessThreshold), (fy * fy) * fy, l / kappa);
                Vector128<float> zr = Vector128.ConditionalSelect(Vector128.GreaterThan(fz3, epsilon), fz3, ((oneHundredSixteen * fz) - sixteen) / kappa);
                Vector128<float> x = xr * Vector128.Create(whitePoint.X);
                Vector128<float> y = yr * Vector128.Create(whitePoint.Y);
                Vector128<float> z = zr * Vector128.Create(whitePoint.Z);

                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        for (; i < source.Length; i++)
        {
            CieLab lab = source[i];
            destination[i] = lab.ToProfileConnectingSpace(options);
        }
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
