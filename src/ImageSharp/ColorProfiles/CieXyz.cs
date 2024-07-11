// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an CIE XYZ 1931 color
/// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space#Definition_of_the_CIE_XYZ_color_space"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieXyz : IProfileConnectingSpace<CieXyz, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CieXyz"/> struct.
    /// </summary>
    /// <param name="x">X is a mix (a linear combination) of cone response curves chosen to be nonnegative</param>
    /// <param name="y">The y luminance component.</param>
    /// <param name="z">Z is quasi-equal to blue stimulation, or the S cone of the human eye.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieXyz(float x, float y, float z)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.X = x;
        this.Y = y;
        this.Z = z;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieXyz"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the x, y, z components.</param>
    public CieXyz(Vector3 vector)
        : this()
    {
        this.X = vector.X;
        this.Y = vector.Y;
        this.Z = vector.Z;
    }

    /// <summary>
    /// Gets the X component. A mix (a linear combination) of cone response curves chosen to be nonnegative.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y luminance component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the Z component. Quasi-equal to blue stimulation, or the S cone response.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float Z { get; }

    /// <summary>
    /// Compares two <see cref="CieXyz"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieXyz"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieXyz"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CieXyz left, CieXyz right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieXyz"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="CieXyz"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieXyz"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CieXyz left, CieXyz right) => !left.Equals(right);

    /// <summary>
    /// Returns a new <see cref="Vector3"/> representing this instance.
    /// </summary>
    /// <returns>The <see cref="Vector3"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ToVector3() => new(this.X, this.Y, this.Z);

    /// <inheritdoc/>
    public static CieXyz FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
        => new(source.X, source.Y, source.Z);

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        source.CopyTo(destination[..source.Length]);
    }

    /// <inheritdoc/>
    public CieXyz ToProfileConnectingSpace(ColorConversionOptions options)
        => new(this.X, this.Y, this.Z);

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        source.CopyTo(destination[..source.Length]);
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource() => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.X, this.Y, this.Z);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"CieXyz({this.X:#0.##}, {this.Y:#0.##}, {this.Z:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CieXyz other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieXyz other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieXyz, Vector3>(ref Unsafe.AsRef(in this));
}
