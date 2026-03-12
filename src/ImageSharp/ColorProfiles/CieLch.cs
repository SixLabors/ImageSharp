// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents the CIE L*C*h°, cylindrical form of the CIE L*a*b* 1976 color.
/// <see href="https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieLch : IColorProfile<CieLch, CieLab>
{
    private static readonly Vector3 Min = new(0, -200, 0);
    private static readonly Vector3 Max = new(100, 200, 360);

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLch"/> struct.
    /// </summary>
    /// <param name="l">The lightness dimension.</param>
    /// <param name="c">The chroma, relative saturation.</param>
    /// <param name="h">The hue in degrees.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLch(float l, float c, float h)
        : this(new Vector3(l, c, h))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLch"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l, c, h components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieLch(Vector3 vector)
    {
        vector = Vector3.Clamp(vector, Min, Max);
        this.L = vector.X;
        this.C = vector.Y;
        this.H = vector.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private CieLch(Vector3 vector, bool _)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
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
    /// <remarks>A value ranging from -200 to 200.</remarks>
    /// </summary>
    public float C { get; }

    /// <summary>
    /// Gets the h° hue component in degrees.
    /// <remarks>A value ranging from 0 to 360.</remarks>
    /// </summary>
    public float H { get; }

    /// <summary>
    /// Compares two <see cref="CieLch"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieLch"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLch"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CieLch left, CieLch right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieLch"/> objects for inequality
    /// </summary>
    /// <param name="left">The <see cref="CieLch"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieLch"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CieLch left, CieLch right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector3 v3 = default;
        v3 += this.AsVector3Unsafe();
        v3 += new Vector3(0, 200, 0);
        v3 /= new Vector3(100, 400, 360);
        return new Vector4(v3, 1F);
    }

    /// <inheritdoc/>
    public static CieLch FromScaledVector4(Vector4 source)
    {
        Vector3 v3 = source.AsVector3();
        v3 *= new Vector3(100, 400, 360);
        v3 -= new Vector3(0, 200, 0);
        return new CieLch(v3, true);
    }

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<CieLch> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<CieLch> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static CieLch FromProfileConnectingSpace(ColorConversionOptions options, in CieLab source)
    {
        // Conversion algorithm described here:
        // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
        float l = source.L, a = source.A, b = source.B;
        float c = MathF.Sqrt((a * a) + (b * b));
        float hRadians = MathF.Atan2(b, a);
        float hDegrees = GeometryUtilities.RadianToDegree(hRadians);

        // Wrap the angle round at 360.
        hDegrees %= 360;

        // Make sure it's not negative.
        while (hDegrees < 0)
        {
            hDegrees += 360;
        }

        return new CieLch(l, c, hDegrees);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLab> source, Span<CieLch> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        for (int i = 0; i < source.Length; i++)
        {
            CieLab lab = source[i];
            destination[i] = FromProfileConnectingSpace(options, in lab);
        }
    }

    /// <inheritdoc/>
    public CieLab ToProfileConnectingSpace(ColorConversionOptions options)
    {
        // Conversion algorithm described here:
        // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
        float l = this.L, c = this.C, hDegrees = this.H;
        float hRadians = GeometryUtilities.DegreeToRadian(hDegrees);

        float a = c * MathF.Cos(hRadians);
        float b = c * MathF.Sin(hRadians);

        return new CieLab(l, a, b);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieLch> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        for (int i = 0; i < source.Length; i++)
        {
            CieLch lch = source[i];
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
    public override string ToString() => FormattableString.Invariant($"CieLch({this.L:#0.##}, {this.C:#0.##}, {this.H:#0.##})");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is CieLch other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieLch other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieLch, Vector3>(ref Unsafe.AsRef(in this));
}
