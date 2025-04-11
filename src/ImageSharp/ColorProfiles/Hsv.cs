// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Hsv : IColorProfile<Hsv, Rgb>
{
    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = new(360, 1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="Hsv"/> struct.
    /// </summary>
    /// <param name="h">The h hue component.</param>
    /// <param name="s">The s saturation component.</param>
    /// <param name="v">The v value (brightness) component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hsv(float h, float s, float v)
        : this(new Vector3(h, s, v))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Hsv"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the h, s, v components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hsv(Vector3 vector)
    {
        vector = Vector3.Clamp(vector, Min, Max);
        this.H = vector.X;
        this.S = vector.Y;
        this.V = vector.Z;
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
    /// Gets the value (brightness) component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float V { get; }

    /// <summary>
    /// Compares two <see cref="Hsv"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Hsv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Hsv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hsv left, Hsv right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Hsv"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Hsv"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Hsv"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hsv left, Hsv right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
        => new(this.AsVector3Unsafe() / 360F, 1F);

    /// <inheritdoc/>
    public static Hsv FromScaledVector4(Vector4 source)
        => new(source.AsVector3() * 360F);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Hsv> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Hsv> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static Hsv FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        float r = source.R;
        float g = source.G;
        float b = source.B;

        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float chroma = max - min;
        float h = 0;
        float s = 0;
        float v = max;

        if (MathF.Abs(chroma) < Constants.Epsilon)
        {
            return new Hsv(0, s, v);
        }

        if (MathF.Abs(r - max) < Constants.Epsilon)
        {
            h = (g - b) / chroma;
        }
        else if (MathF.Abs(g - max) < Constants.Epsilon)
        {
            h = 2 + ((b - r) / chroma);
        }
        else if (MathF.Abs(b - max) < Constants.Epsilon)
        {
            h = 4 + ((r - g) / chroma);
        }

        h *= 60F;
        if (h < -Constants.Epsilon)
        {
            h += 360F;
        }

        s = chroma / v;

        return new Hsv(h, s, v);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Hsv> destination)
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
        float s = this.S;
        float v = this.V;

        if (MathF.Abs(s) < Constants.Epsilon)
        {
            return new Rgb(v, v, v);
        }

        float h = (MathF.Abs(this.H - 360) < Constants.Epsilon) ? 0 : this.H / 60;
        int i = (int)Math.Truncate(h);
        float f = h - i;

        float p = v * (1F - s);
        float q = v * (1F - (s * f));
        float t = v * (1F - (s * (1F - f)));

        float r, g, b;
        switch (i)
        {
            case 0:
                r = v;
                g = t;
                b = p;
                break;

            case 1:
                r = q;
                g = v;
                b = p;
                break;

            case 2:
                r = p;
                g = v;
                b = t;
                break;

            case 3:
                r = p;
                g = q;
                b = v;
                break;

            case 4:
                r = t;
                g = p;
                b = v;
                break;

            default:
                r = v;
                g = p;
                b = q;
                break;
        }

        return new Rgb(r, g, b);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Hsv> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            Hsv hsv = source[i];
            destination[i] = hsv.ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(this.H, this.S, this.V);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Hsv({this.H:#0.##}, {this.S:#0.##}, {this.V:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Hsv other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hsv other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<Hsv, Vector3>(ref Unsafe.AsRef(in this));
}
