// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents the CIE L*C*h°, cylindrical form of the CIE L*u*v* 1976 color.
/// <see href="https://en.wikipedia.org/wiki/CIELAB_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieLchuv : IColorProfile<CieLchuv, CieXyz>
{
    private static readonly Vector3 Min = new(0, -200, 0);
    private static readonly Vector3 Max = new(100, 200, 360);

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
    /// </summary>
    /// <param name="l">The lightness dimension.</param>
    /// <param name="c">The chroma, relative saturation.</param>
    /// <param name="h">The hue in degrees.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLchuv(float l, float c, float h)
       : this(new(l, c, h))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l, c, h components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLchuv(Vector3 vector)
        : this()
    {
        vector = Vector3.Clamp(vector, Min, Max);
        this.L = vector.X;
        this.C = vector.Y;
        this.H = vector.Z;
    }

    /// <summary>
    /// Gets the lightness dimension.
    /// <remarks>A value ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
    /// </summary>
    public float L { get; }

    /// <summary>
    /// Gets the a chroma component.
    /// <remarks>A value ranging from 0 to 200.</remarks>
    /// </summary>
    public float C { get; }

    /// <summary>
    /// Gets the h° hue component in degrees.
    /// <remarks>A value ranging from 0 to 360.</remarks>
    /// </summary>
    public float H { get; }

    /// <summary>
    /// Compares two <see cref="CieLchuv"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieLchuv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLchuv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator ==(CieLchuv left, CieLchuv right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieLchuv"/> objects for inequality
    /// </summary>
    /// <param name="left">The <see cref="CieLchuv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLchuv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator !=(CieLchuv left, CieLchuv right) => !left.Equals(right);

    /// <inheritdoc/>
    public static CieLchuv FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        CieLuv luv = CieLuv.FromProfileConnectingSpace(options, source);

        // Conversion algorithm described here:
        // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
        float l = luv.L, u = luv.U, v = luv.V;
        float c = MathF.Sqrt((u * u) + (v * v));
        float hRadians = MathF.Atan2(v, u);
        float hDegrees = GeometryUtilities.RadianToDegree(hRadians);

        // Wrap the angle round at 360.
        hDegrees %= 360;

        // Make sure it's not negative.
        while (hDegrees < 0)
        {
            hDegrees += 360;
        }

        return new(l, c, hDegrees);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieLchuv> destination)
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
        // Conversion algorithm described here:
        // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
        float l = this.L, c = this.C, hDegrees = this.H;
        float hRadians = GeometryUtilities.DegreeToRadian(hDegrees);

        float u = c * MathF.Cos(hRadians);
        float v = c * MathF.Sin(hRadians);

        CieLuv luv = new(l, u, v);
        return luv.ToProfileConnectingSpace(options);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLchuv> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            CieLchuv lch = source[i];
            destination[i] = lch.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.L, this.C, this.H);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"CieLchuv({this.L:#0.##}, {this.C:#0.##}, {this.H:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is CieLchuv other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieLchuv other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieLchuv, Vector3>(ref Unsafe.AsRef(in this));
}
