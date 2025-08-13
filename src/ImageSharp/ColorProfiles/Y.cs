// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents a Y (luminance) color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Y : IColorProfile<Y, Rgb>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Y"/> struct.
    /// </summary>
    /// <param name="l">The luminance component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Y(float l) => this.L = Numerics.Clamp(l, 0, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private Y(float l, bool _) => this.L = l;
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter

    /// <summary>
    /// Gets the luminance component.
    /// </summary>
    /// <remarks>A value ranging between 0 and 1.</remarks>
    public float L { get; }

    /// <summary>
    /// Compares two <see cref="Y"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Y"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Y"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Y left, Y right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Y"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Y"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Y"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Y left, Y right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4() => new(this.L);

    /// <inheritdoc/>
    public static Y FromScaledVector4(Vector4 source) => new(source.X, true);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Y> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Y> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
        => new(this.L, this.L, this.L);

    /// <inheritdoc/>
    public static Y FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        Matrix4x4 m = options.YCbCrTransform.Forward;
        float offset = options.YCbCrTransform.Offset.X;
        return new Y(Vector3.Dot(source.AsVector3Unsafe(), new Vector3(m.M11, m.M12, m.M13)) + offset);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Y> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: We can optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Y> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: We can optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            Rgb rgb = source[i];
            destination[i] = FromProfileConnectingSpace(options, in rgb);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => this.L.GetHashCode();

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"Y({this.L:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Y other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(Y other) => this.L == other.L;
}
