// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// LMS is a color space represented by the response of the three types of cones of the human eye,
/// named after their responsivity (sensitivity) at long, medium and short wavelengths.
/// <see href="https://en.wikipedia.org/wiki/LMS_color_space"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Lms : IColorProfile<Lms, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Lms"/> struct.
    /// </summary>
    /// <param name="l">L represents the responsivity at long wavelengths.</param>
    /// <param name="m">M represents the responsivity at medium wavelengths.</param>
    /// <param name="s">S represents the responsivity at short wavelengths.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Lms(float l, float m, float s)
    {
        this.L = l;
        this.M = m;
        this.S = s;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Lms"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the l, m, s components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Lms(Vector3 vector)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.L = vector.X;
        this.M = vector.Y;
        this.S = vector.Z;
    }

    /// <summary>
    /// Gets the L long component.
    /// <remarks>A value usually ranging between -1 and 1.</remarks>
    /// </summary>
    public float L { get; }

    /// <summary>
    /// Gets the M medium component.
    /// <remarks>A value usually ranging between -1 and 1.</remarks>
    /// </summary>
    public float M { get; }

    /// <summary>
    /// Gets the S short component.
    /// <remarks>A value usually ranging between -1 and 1.</remarks>
    /// </summary>
    public float S { get; }

    /// <summary>
    /// Compares two <see cref="Lms"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Lms"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Lms"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Lms left, Lms right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Lms"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Lms"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Lms"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Lms left, Lms right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector3 v3 = default;
        v3 += this.AsVector3Unsafe();
        v3 += new Vector3(1F);
        v3 /= 2F;
        return new Vector4(v3, 1F);
    }

    /// <inheritdoc/>
    public static Lms FromScaledVector4(Vector4 source)
    {
        Vector3 v3 = source.AsVector3();
        v3 *= 2F;
        v3 -= new Vector3(1F);
        return new Lms(v3);
    }

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Lms> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Lms> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static Lms FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
        => new(Vector3.Transform(source.AsVector3Unsafe(), options.AdaptationMatrix));

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<Lms> destination)
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
        => new(Vector3.Transform(this.AsVector3Unsafe(), options.InverseAdaptationMatrix));

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Lms> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        for (int i = 0; i < source.Length; i++)
        {
            Lms lms = source[i];
            destination[i] = lms.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource() => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.L, this.M, this.S);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Lms({this.L:#0.##}, {this.M:#0.##}, {this.S:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Lms other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Lms other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<Lms, Vector3>(ref Unsafe.AsRef(in this));
}
