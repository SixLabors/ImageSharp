// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an Hunter LAB color.
/// <see href="https://en.wikipedia.org/wiki/Lab_color_space"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HunterLab : IColorProfile<HunterLab, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HunterLab"/> struct.
    /// </summary>
    /// <param name="l">The lightness dimension.</param>
    /// <param name="a">The a (green - magenta) component.</param>
    /// <param name="b">The b (blue - yellow) component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HunterLab(float l, float a, float b)
    {
        this.L = l;
        this.A = a;
        this.B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HunterLab"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l a b components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HunterLab(Vector3 vector)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
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
    /// Compares two <see cref="HunterLab"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="HunterLab"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HunterLab"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator ==(HunterLab left, HunterLab right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="HunterLab"/> objects for inequality
    /// </summary>
    /// <param name="left">The <see cref="HunterLab"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HunterLab"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HunterLab left, HunterLab right) => !left.Equals(right);

    /// <inheritdoc/>
    public static HunterLab FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        // Conversion algorithm described here:
        // http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
        CieXyz whitePoint = options.TargetWhitePoint;
        float x = source.X, y = source.Y, z = source.Z;
        float xn = whitePoint.X, yn = whitePoint.Y, zn = whitePoint.Z;

        float ka = ComputeKa(in whitePoint);
        float kb = ComputeKb(in whitePoint);

        float yByYn = y / yn;
        float sqrtYbyYn = MathF.Sqrt(yByYn);
        float l = 100 * sqrtYbyYn;
        float a = ka * (((x / xn) - yByYn) / sqrtYbyYn);
        float b = kb * ((yByYn - (z / zn)) / sqrtYbyYn);

        if (float.IsNaN(a))
        {
            a = 0;
        }

        if (float.IsNaN(b))
        {
            b = 0;
        }

        return new(l, a, b);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<HunterLab> destination)
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
        // http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
        CieXyz whitePoint = options.WhitePoint;
        float l = this.L, a = this.A, b = this.B;
        float xn = whitePoint.X, yn = whitePoint.Y, zn = whitePoint.Z;

        float ka = ComputeKa(in whitePoint);
        float kb = ComputeKb(in whitePoint);

        float pow = Numerics.Pow2(l / 100F);
        float sqrtPow = MathF.Sqrt(pow);
        float y = pow * yn;

        float x = (((a / ka) * sqrtPow) + pow) * xn;
        float z = (((b / kb) * sqrtPow) - pow) * (-zn);

        return new(x, y, z);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<HunterLab> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            HunterLab lab = source[i];
            destination[i] = lab.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(this.L, this.A, this.B);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"HunterLab({this.L:#0.##}, {this.A:#0.##}, {this.B:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HunterLab other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(HunterLab other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<HunterLab, Vector3>(ref Unsafe.AsRef(in this));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeKa(in CieXyz whitePoint)
    {
        if (whitePoint.Equals(KnownIlluminants.C))
        {
            return 175F;
        }

        return 100F * (175F / 198.04F) * (whitePoint.X + whitePoint.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeKb(in CieXyz whitePoint)
    {
        if (whitePoint == KnownIlluminants.C)
        {
            return 70F;
        }

        return 100F * (70F / 218.11F) * (whitePoint.Y + whitePoint.Z);
    }
}
