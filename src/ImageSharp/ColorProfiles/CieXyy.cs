// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an CIE xyY 1931 color
/// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space#CIE_xy_chromaticity_diagram_and_the_CIE_xyY_color_space"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CieXyy : IColorProfile<CieXyy, CieXyz>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CieXyy"/> struct.
    /// </summary>
    /// <param name="x">The x chroma component.</param>
    /// <param name="y">The y chroma component.</param>
    /// <param name="yl">The y luminance component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieXyy(float x, float y, float yl)
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.X = x;
        this.Y = y;
        this.Yl = yl;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CieXyy"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the x, y, Y components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CieXyy(Vector3 vector)
        : this()
    {
        // Not clamping as documentation about this space only indicates "usual" ranges
        this.X = vector.X;
        this.Y = vector.Y;
        this.Yl = vector.Z;
    }

    /// <summary>
    /// Gets the X chrominance component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y chrominance component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the Y luminance component.
    /// <remarks>A value usually ranging between 0 and 1.</remarks>
    /// </summary>
    public float Yl { get; }

    /// <summary>
    /// Compares two <see cref="CieXyy"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="CieXyy"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieXyy"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CieXyy left, CieXyy right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="CieXyy"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="CieXyy"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="CieXyy"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CieXyy left, CieXyy right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
        => new(this.AsVector3Unsafe(), 1F);

    /// <inheritdoc/>
    public static CieXyy FromScaledVector4(Vector4 source)
        => new(source.AsVector3());

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<CieXyy> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static CieXyy FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        float x = source.X / (source.X + source.Y + source.Z);
        float y = source.Y / (source.X + source.Y + source.Z);

        if (float.IsNaN(x) || float.IsNaN(y))
        {
            return new CieXyy(0, 0, source.Y);
        }

        return new CieXyy(x, y, source.Y);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieXyy> destination)
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
        if (MathF.Abs(this.Y) < Constants.Epsilon)
        {
            return new CieXyz(0, 0, this.Yl);
        }

        float x = (this.X * this.Yl) / this.Y;
        float y = this.Yl;
        float z = ((1 - this.X - this.Y) * y) / this.Y;

        return new CieXyz(x, y, z);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyy> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            CieXyy xyz = source[i];
            destination[i] = xyz.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.WhitePoint;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.X, this.Y, this.Yl);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"CieXyy({this.X:#0.##}, {this.Y:#0.##}, {this.Yl:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CieXyy other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CieXyy other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<CieXyy, Vector3>(ref Unsafe.AsRef(in this));
}
