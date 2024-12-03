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
        : this()
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
        Vector3 v3 = source.AsVector128().AsVector3();
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

        return new(xyzr * wxyz);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLab> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        for (int i = 0; i < source.Length; i++)
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
