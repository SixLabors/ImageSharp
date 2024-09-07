// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an CMYK (cyan, magenta, yellow, keyline) color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Cmyk : IColorProfile<Cmyk, Rgb>
{
    private static readonly Vector4 Min = Vector4.Zero;
    private static readonly Vector4 Max = Vector4.One;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmyk"/> struct.
    /// </summary>
    /// <param name="c">The cyan component.</param>
    /// <param name="m">The magenta component.</param>
    /// <param name="y">The yellow component.</param>
    /// <param name="k">The keyline black component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmyk(float c, float m, float y, float k)
        : this(new(c, m, y, k))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmyk"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the c, m, y, k components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmyk(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Min, Max);
        this.C = vector.X;
        this.M = vector.Y;
        this.Y = vector.Z;
        this.K = vector.W;
    }

    /// <summary>
    /// Gets the cyan color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float C { get; }

    /// <summary>
    /// Gets the magenta color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float M { get; }

    /// <summary>
    /// Gets the yellow color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the keyline black color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float K { get; }

    /// <summary>
    /// Compares two <see cref="Cmyk"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Cmyk left, Cmyk right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Cmyk"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Cmyk left, Cmyk right) => !left.Equals(right);

    /// <inheritdoc/>
    public static Cmyk FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        // To CMY
        Vector3 cmy = Vector3.One - source.ToScaledVector3();

        // To CMYK
        Vector3 k = new(MathF.Min(cmy.X, MathF.Min(cmy.Y, cmy.Z)));

        if (MathF.Abs(k.X - 1F) < Constants.Epsilon)
        {
            return new(0, 0, 0, 1F);
        }

        cmy = (cmy - k) / (Vector3.One - k);

        return new(cmy.X, cmy.Y, cmy.Z, k.X);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Cmyk> destination)
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
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
    {
        Vector3 rgb = (Vector3.One - new Vector3(this.C, this.M, this.Y)) * (Vector3.One - new Vector3(this.K));
        return Rgb.FromScaledVector3(rgb);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Cmyk> source, Span<Rgb> destination)
    {
        // TODO: We can possibly optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            Cmyk cmyk = source[i];
            destination[i] = cmyk.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => HashCode.Combine(this.C, this.M, this.Y, this.K);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"Cmyk({this.C:#0.##}, {this.M:#0.##}, {this.Y:#0.##}, {this.K:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Cmyk other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Cmyk other)
        => this.AsVector4Unsafe() == other.AsVector4Unsafe();

    private Vector4 AsVector4Unsafe() => Unsafe.As<Cmyk, Vector4>(ref Unsafe.AsRef(in this));
}
