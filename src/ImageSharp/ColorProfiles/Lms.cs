// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorProfiles;

internal readonly struct Lms : IColorProfile<Lms, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Lms"/> struct.
    /// </summary>
    /// <param name="l">L represents the responsivity at long wavelengths.</param>
    /// <param name="m">M represents the responsivity at medium wavelengths.</param>
    /// <param name="s">S represents the responsivity at short wavelengths.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Lms(float l, float m, float s)
        : this(new Vector3(l, m, s))
    {
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
    public readonly float L { get; }

    /// <summary>
    /// Gets the M medium component.
    /// <remarks>A value usually ranging between -1 and 1.</remarks>
    /// </summary>
    public readonly float M { get; }

    /// <summary>
    /// Gets the S short component.
    /// <remarks>A value usually ranging between -1 and 1.</remarks>
    /// </summary>
    public readonly float S { get; }

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

    /// <summary>
    /// Returns a new <see cref="Vector3"/> representing this instance.
    /// </summary>
    /// <returns>The <see cref="Vector3"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ToVector3() => new(this.L, this.M, this.S);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.L, this.M, this.S);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Lms({this.L:#0.##}, {this.M:#0.##}, {this.S:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Lms other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Lms other)
        => this.L.Equals(other.L)
        && this.M.Equals(other.M)
        && this.S.Equals(other.S);

    /// <inheritdoc/>
    public static Lms FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        Vector3 vector = Vector3.Transform(source.ToVector3(), options.AdaptationMatrix);
        return new Lms(vector);
    }

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
    {
        Vector3 vector = Vector3.Transform(this.ToVector3(), options.InverseAdaptationMatrix);
        return new CieXyz(vector);
    }

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
}
