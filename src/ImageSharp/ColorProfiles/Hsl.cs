// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents a Hsl (hue, saturation, lightness) color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Hsl : IColorProfile<Hsl, Rgb>
{
    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = new(360, 1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="Hsl"/> struct.
    /// </summary>
    /// <param name="h">The h hue component.</param>
    /// <param name="s">The s saturation component.</param>
    /// <param name="l">The l value (lightness) component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hsl(float h, float s, float l)
        : this(new(h, s, l))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Hsl"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the h, s, l components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hsl(Vector3 vector)
    {
        vector = Vector3.Clamp(vector, Min, Max);
        this.H = vector.X;
        this.S = vector.Y;
        this.L = vector.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private Hsl(Vector3 vector, bool _)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    {
        this.H = vector.X;
        this.S = vector.Y;
        this.L = vector.Z;
    }

    /// <summary>
    /// Gets the hue component.
    /// <remarks>A value ranging between 0 and 360.</remarks>
    /// </summary>
    public float H { get; }

    /// <summary>
    /// Gets the saturation component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float S { get; }

    /// <summary>
    /// Gets the lightness component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float L { get; }

    /// <summary>
    /// Compares two <see cref="Hsl"/> objects for equality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="Hsl"/> on the left side of the operand.
    /// </param>
    /// <param name="right">The <see cref="Hsl"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hsl left, Hsl right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Hsl"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Hsl"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Hsl"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hsl left, Hsl right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
        => new(this.AsVector3Unsafe() / 360F, 1F);

    /// <inheritdoc/>
    public static Hsl FromScaledVector4(Vector4 source)
        => new(source.AsVector3() * 360F, true);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Hsl> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Hsl> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static Hsl FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        float r = source.R;
        float g = source.G;
        float b = source.B;

        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float chroma = max - min;
        float h = 0F;
        float s = 0F;
        float l = (max + min) / 2F;

        if (MathF.Abs(chroma) < Constants.Epsilon)
        {
            return new(0F, s, l);
        }

        if (MathF.Abs(r - max) < Constants.Epsilon)
        {
            h = (g - b) / chroma;
        }
        else if (MathF.Abs(g - max) < Constants.Epsilon)
        {
            h = 2F + ((b - r) / chroma);
        }
        else if (MathF.Abs(b - max) < Constants.Epsilon)
        {
            h = 4F + ((r - g) / chroma);
        }

        h *= 60F;
        if (h < -Constants.Epsilon)
        {
            h += 360F;
        }

        if (l <= .5F)
        {
            s = chroma / (max + min);
        }
        else
        {
            s = chroma / (2F - max - min);
        }

        return new(h, s, l);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Hsl> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            Rgb rgb = source[i];
            destination[i] = FromProfileConnectingSpace(options, in rgb);
        }
    }

    /// <inheritdoc/>
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
    {
        float rangedH = this.H / 360F;
        float r = 0;
        float g = 0;
        float b = 0;
        float s = this.S;
        float l = this.L;

        if (MathF.Abs(l) > Constants.Epsilon)
        {
            if (MathF.Abs(s) < Constants.Epsilon)
            {
                r = g = b = l;
            }
            else
            {
                float temp2 = (l < .5F) ? l * (1F + s) : l + s - (l * s);
                float temp1 = (2F * l) - temp2;

                r = GetColorComponent(temp1, temp2, rangedH + 0.3333333F);
                g = GetColorComponent(temp1, temp2, rangedH);
                b = GetColorComponent(temp1, temp2, rangedH - 0.3333333F);
            }
        }

        return new(r, g, b);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Hsl> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            Hsl hsl = source[i];
            destination[i] = hsl.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(this.H, this.S, this.L);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Hsl({this.H:#0.##}, {this.S:#0.##}, {this.L:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Hsl other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hsl other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<Hsl, Vector3>(ref Unsafe.AsRef(in this));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetColorComponent(float first, float second, float third)
    {
        third = MoveIntoRange(third);
        if (third < 0.1666667F)
        {
            return first + ((second - first) * 6F * third);
        }

        if (third < .5F)
        {
            return second;
        }

        if (third < 0.6666667F)
        {
            return first + ((second - first) * (0.6666667F - third) * 6F);
        }

        return first;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float MoveIntoRange(float value)
    {
        if (value < 0F)
        {
            value++;
        }
        else if (value > 1F)
        {
            value--;
        }

        return value;
    }
}
