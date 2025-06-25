// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Represents a color value that is convertible to any <see cref="IPixel{TSelf}"/> type.
/// </summary>
/// <remarks>
/// The internal representation and layout of this structure is hidden by intention.
/// It's not serializable, and it should not be considered as part of a contract.
/// Unlike System.Drawing.Color, <see cref="Color"/> has to be converted to a specific pixel value
/// to query the color components.
/// </remarks>
public readonly partial struct Color : IEquatable<Color>
{
    private readonly Vector4 data;
    private readonly IPixel? boxedHighPrecisionPixel;

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="vector">The <see cref="Vector4"/> containing the color information.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color(Vector4 vector)
    {
        this.data = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
        this.boxedHighPrecisionPixel = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="pixel">The pixel containing color information.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color(IPixel pixel)
    {
        this.boxedHighPrecisionPixel = pixel;
        this.data = default;
    }

    /// <summary>
    /// Checks whether two <see cref="Color"/> structures are equal.
    /// </summary>
    /// <param name="left">The left hand <see cref="Color"/> operand.</param>
    /// <param name="right">The right hand <see cref="Color"/> operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter;
    /// otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>
    /// Checks whether two <see cref="Color"/> structures are not equal.
    /// </summary>
    /// <param name="left">The left hand <see cref="Color"/> operand.</param>
    /// <param name="right">The right hand <see cref="Color"/> operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
    /// otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    /// <summary>
    /// Creates a <see cref="Color"/> from the given <typeparamref name="TPixel"/>.
    /// </summary>
    /// <param name="source">The pixel to convert from.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>The <see cref="Color"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color FromPixel<TPixel>(TPixel source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Avoid boxing in case we can convert to Vector4 safely and efficiently
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        if (info.ComponentInfo.HasValue && info.ComponentInfo.Value.GetMaximumComponentPrecision() <= (int)PixelComponentBitDepth.Bit32)
        {
            return new Color(source.ToScaledVector4());
        }

        return new Color(source);
    }

    /// <summary>
    /// Creates a <see cref="Color"/> from a generic scaled <see cref="Vector4"/>.
    /// </summary>
    /// <param name="source">The vector to load the pixel from.</param>
    /// <returns>The <see cref="Color"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color FromScaledVector(Vector4 source) => new(source);

    /// <summary>
    /// Bulk converts a span of a specified <typeparamref name="TPixel"/> type to a span of <see cref="Color"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
    /// <param name="source">The source pixel span.</param>
    /// <param name="destination">The destination color span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromPixel<TPixel>(ReadOnlySpan<TPixel> source, Span<Color> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Avoid boxing in case we can convert to Vector4 safely and efficiently
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        if (info.ComponentInfo.HasValue && info.ComponentInfo.Value.GetMaximumComponentPrecision() <= (int)PixelComponentBitDepth.Bit32)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = FromScaledVector(source[i].ToScaledVector4());
            }
        }
        else
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = new Color(source[i]);
            }
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Color"/> struct
    /// from the given hexadecimal string.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components arranged
    /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
    /// </param>
    /// <returns>
    /// The <see cref="Color"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color ParseHex(string hex)
    {
        Rgba32 rgba = Rgba32.ParseHex(hex);
        return FromPixel(rgba);
    }

    /// <summary>
    /// Attempts to creates a new instance of the <see cref="Color"/> struct
    /// from the given hexadecimal string.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components arranged
    /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
    /// </param>
    /// <param name="result">When this method returns, contains the <see cref="Color"/> equivalent of the hexadecimal input.</param>
    /// <returns>
    /// The <see cref="bool"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHex(string hex, out Color result)
    {
        result = default;

        if (Rgba32.TryParseHex(hex, out Rgba32 rgba))
        {
            result = FromPixel(rgba);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Color"/> struct
    /// from the given input string.
    /// </summary>
    /// <param name="input">
    /// The name of the color or the hexadecimal representation of the combined color components arranged
    /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
    /// </param>
    /// <returns>
    /// The <see cref="Color"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Input string is not in the correct format.</exception>
    public static Color Parse(string input)
    {
        Guard.NotNull(input, nameof(input));

        if (!TryParse(input, out Color color))
        {
            throw new ArgumentException("Input string is not in the correct format.", nameof(input));
        }

        return color;
    }

    /// <summary>
    /// Attempts to creates a new instance of the <see cref="Color"/> struct
    /// from the given input string.
    /// </summary>
    /// <param name="input">
    /// The name of the color or the hexadecimal representation of the combined color components arranged
    /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
    /// </param>
    /// <param name="result">When this method returns, contains the <see cref="Color"/> equivalent of the hexadecimal input.</param>
    /// <returns>
    /// The <see cref="bool"/>.
    /// </returns>
    public static bool TryParse(string input, out Color result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (NamedColorsLookupLazy.Value.TryGetValue(input, out result))
        {
            return true;
        }

        return TryParseHex(input, out result);
    }

    /// <summary>
    /// Alters the alpha channel of the color, returning a new instance.
    /// </summary>
    /// <param name="alpha">The new value of alpha [0..1].</param>
    /// <returns>The color having it's alpha channel altered.</returns>
    public Color WithAlpha(float alpha)
    {
        Vector4 v = this.ToScaledVector4();
        v.W = alpha;
        return FromScaledVector(v);
    }

    /// <summary>
    /// Gets the hexadecimal representation of the color instance in rrggbbaa form.
    /// </summary>
    /// <returns>A hexadecimal string representation of the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToHex()
    {
        if (this.boxedHighPrecisionPixel is not null)
        {
            return this.boxedHighPrecisionPixel.ToRgba32().ToHex();
        }

        return Rgba32.FromScaledVector4(this.data).ToHex();
    }

    /// <inheritdoc />
    public override string ToString() => this.ToHex();

    /// <summary>
    /// Converts the color instance to a specified <typeparamref name="TPixel"/> type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
    /// <returns>The <typeparamref name="TPixel"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TPixel ToPixel<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.boxedHighPrecisionPixel is TPixel pixel)
        {
            return pixel;
        }

        if (this.boxedHighPrecisionPixel is null)
        {
            return TPixel.FromScaledVector4(this.data);
        }

        return TPixel.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
    }

    /// <summary>
    /// Expands the color into a generic ("scaled") <see cref="Vector4"/> representation
    /// with values scaled and clamped between <value>0</value> and <value>1</value>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToScaledVector4()
    {
        if (this.boxedHighPrecisionPixel is null)
        {
            return this.data;
        }

        return this.boxedHighPrecisionPixel.ToScaledVector4();
    }

    /// <summary>
    /// Bulk converts a span of <see cref="Color"/> to a span of a specified <typeparamref name="TPixel"/> type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
    /// <param name="source">The source color span.</param>
    /// <param name="destination">The destination pixel span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToPixel<TPixel>(ReadOnlySpan<Color> source, Span<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // We cannot use bulk pixel operations here as there is no guarantee that the source colors are
        // created from pixel formats which fit into the unboxed vector data.
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToPixel<TPixel>();
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Color other)
    {
        if (this.boxedHighPrecisionPixel is null && other.boxedHighPrecisionPixel is null)
        {
            return this.data == other.data;
        }

        return this.boxedHighPrecisionPixel?.Equals(other.boxedHighPrecisionPixel) == true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && this.Equals(other);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (this.boxedHighPrecisionPixel is null)
        {
            return this.data.GetHashCode();
        }

        return this.boxedHighPrecisionPixel.GetHashCode();
    }
}
