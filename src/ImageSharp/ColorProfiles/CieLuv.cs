// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// The CIE 1976 (L*, u*, v*) color space, commonly known by its abbreviation CIELUV, is a color space adopted by the International
/// Commission on Illumination (CIE) in 1976, as a simple-to-compute transformation of the 1931 CIE XYZ color space, but which
/// attempted perceptual uniformity
/// <see href="https://en.wikipedia.org/wiki/CIELUV"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieLuv : IColorProfile<CieLuv, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CieLuv"/> struct.
    /// </summary>
    /// <param name="l">The lightness dimension.</param>
    /// <param name="u">The blue-yellow chromaticity coordinate of the given white point.</param>
    /// <param name="v">The red-green chromaticity coordinate of the given white point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLuv(float l, float u, float v)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.L = l;
        this.U = u;
        this.V = v;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLuv"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l, u, v components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLuv(Vector3 vector)
        : this()
    {
        this.L = vector.X;
        this.U = vector.Y;
        this.V = vector.Z;
    }

    /// <summary>
    /// Gets the lightness dimension
    /// <remarks>A value usually ranging between 0 and 100.</remarks>
    /// </summary>
    public float L { get; }

    /// <summary>
    /// Gets the blue-yellow chromaticity coordinate of the given white point.
    /// <remarks>A value usually ranging between -100 and 100.</remarks>
    /// </summary>
    public float U { get; }

    /// <summary>
    /// Gets the red-green chromaticity coordinate of the given white point.
    /// <remarks>A value usually ranging between -100 and 100.</remarks>
    /// </summary>
    public float V { get; }

    /// <summary>
    /// Compares two <see cref="CieLuv"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieLuv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLuv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CieLuv left, CieLuv right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieLuv"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="CieLuv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLuv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CieLuv left, CieLuv right) => !left.Equals(right);

    /// <inheritdoc/>
    public static CieLuv FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        // Use doubles here for accuracy.
        // Conversion algorithm described here:
        // http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Luv.html
        CieXyz whitePoint = options.TargetWhitePoint;

        double yr = source.Y / whitePoint.Y;

        double den = source.X + (15 * source.Y) + (3 * source.Z);
        double up = den > 0 ? ComputeU(in source) : 0;
        double vp = den > 0 ? ComputeV(in source) : 0;
        double upr = ComputeU(in whitePoint);
        double vpr = ComputeV(in whitePoint);

        const double e = 1 / 3d;
        double l = yr > CieConstants.Epsilon
            ? ((116 * Math.Pow(yr, e)) - 16d)
            : (CieConstants.Kappa * yr);

        if (double.IsNaN(l) || l == -0d)
        {
            l = 0;
        }

        double u = 13 * l * (up - upr);
        double v = 13 * l * (vp - vpr);

        if (double.IsNaN(u) || u == -0d)
        {
            u = 0;
        }

        if (double.IsNaN(v) || v == -0d)
        {
            v = 0;
        }

        return new((float)l, (float)u, (float)v);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieLuv> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            CieXyz xyz = source[i];
            destination[i] = FromProfileConnectingSpace(options, in xyz);
        }
    }

    /// <inheritdoc/>
    public CieXyz ToProfileConnectingSpace(ColorConversionOptions options)
    {
        // Use doubles here for accuracy.
        // Conversion algorithm described here:
        // http://www.brucelindbloom.com/index.html?Eqn_Luv_to_XYZ.html
        CieXyz whitePoint = options.WhitePoint;

        double l = this.L, u = this.U, v = this.V;

        double u0 = ComputeU(in whitePoint);
        double v0 = ComputeV(in whitePoint);

        double y = l > CieConstants.Kappa * CieConstants.Epsilon
                    ? Numerics.Pow3((l + 16) / 116d)
                    : l / CieConstants.Kappa;

        double a = ((52 * l / (u + (13 * l * u0))) - 1) / 3;
        double b = -5 * y;
        const double c = -1 / 3d;
        double d = y * ((39 * l / (v + (13 * l * v0))) - 5);

        double x = (d - b) / (a - c);
        double z = (x * a) + b;

        if (double.IsNaN(x) || x == -0d)
        {
            x = 0;
        }

        if (double.IsNaN(y) || y == -0d)
        {
            y = 0;
        }

        if (double.IsNaN(z) || z == -0d)
        {
            z = 0;
        }

        return new((float)x, (float)y, (float)z);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLuv> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            CieLuv luv = source[i];
            destination[i] = luv.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.L, this.U, this.V);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"CieLuv({this.L:#0.##}, {this.U:#0.##}, {this.V:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CieLuv other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieLuv other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieLuv, Vector3>(ref Unsafe.AsRef(in this));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeU(in CieXyz source)
        => (4 * source.X) / (source.X + (15 * source.Y) + (3 * source.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeV(in CieXyz source)
       => (9 * source.Y) / (source.X + (15 * source.Y) + (3 * source.Z));
}
