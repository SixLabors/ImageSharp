// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
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
    /// Gets a <see cref="Color"/> from the given hexadecimal string.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="format">
    /// The format of the hexadecimal string to parse, if applicable. Defaults to <see cref="ColorHexFormat.Rgba"/>.
    /// </param>
    /// <returns>
    /// The <see cref="Color"/> equivalent of the hexadecimal input.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="hex"/> is not in the correct format.
    /// </exception>
    public static Color ParseHex(string hex, ColorHexFormat format = ColorHexFormat.Rgba)
    {
        Guard.NotNull(hex, nameof(hex));

        if (!TryParseHex(hex, out Color color, format))
        {
            throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
        }

        return color;
    }

    /// <summary>
    /// Gets a <see cref="Color"/> from the given hexadecimal string.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="Color"/> equivalent of the hexadecimal input.
    /// </param>
    /// <param name="format">
    /// The format of the hexadecimal string to parse, if applicable. Defaults to <see cref="ColorHexFormat.Rgba"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParseHex(string hex, out Color result, ColorHexFormat format = ColorHexFormat.Rgba)
    {
        result = default;

        if (format == ColorHexFormat.Argb)
        {
            if (TryParseArgbHex(hex, out Argb32 argb))
            {
                result = FromPixel(argb);
                return true;
            }
        }
        else if (format == ColorHexFormat.Rgba)
        {
            if (TryParseRgbaHex(hex, out Rgba32 rgba))
            {
                result = FromPixel(rgba);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a <see cref="Color"/> from the given input string.
    /// </summary>
    /// <param name="input">
    /// The name of the color or the hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="format">
    /// The format of the hexadecimal string to parse, if applicable. Defaults to <see cref="ColorHexFormat.Rgba"/>.
    /// </param>
    /// <returns>
    /// The <see cref="Color"/> equivalent of the input string.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="input"/> is not in the correct format.
    /// </exception>
    public static Color Parse(string input, ColorHexFormat format = ColorHexFormat.Rgba)
    {
        Guard.NotNull(input, nameof(input));

        if (!TryParse(input, out Color color, format))
        {
            throw new ArgumentException("Input string is not in the correct format.", nameof(input));
        }

        return color;
    }

    /// <summary>
    /// Tries to create a new instance of the <see cref="Color"/> struct from the given input string.
    /// </summary>
    /// <param name="input">
    /// The name of the color or the hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="Color"/> equivalent of the input string.
    /// </param>
    /// <param name="format">
    /// The format of the hexadecimal string to parse, if applicable. Defaults to <see cref="ColorHexFormat.Rgba"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string input, out Color result, ColorHexFormat format = ColorHexFormat.Rgba)
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

        result = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return TryParseHex(input, out result, format);
    }

    /// <summary>
    /// Alters the alpha channel of the color, returning a new instance.
    /// </summary>
    /// <param name="alpha">The new value of alpha [0..1].</param>
    /// <returns>The color having it's alpha channel altered.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color WithAlpha(float alpha)
    {
        Vector4 v = this.ToScaledVector4();
        v.W = alpha;
        return FromScaledVector(v);
    }

    /// <summary>
    /// Gets the hexadecimal string representation of the color instance.
    /// </summary>
    /// <param name="format">
    /// The format of the hexadecimal string to return. Defaults to <see cref="ColorHexFormat.Rgba"/>.
    /// </param>
    /// <returns>A hexadecimal string representation of the value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="format"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToHex(ColorHexFormat format = ColorHexFormat.Rgba)
    {
        Rgba32 rgba = (this.boxedHighPrecisionPixel is not null)
            ? this.boxedHighPrecisionPixel.ToRgba32()
            : Rgba32.FromScaledVector4(this.data);

        uint hexOrder = format switch
        {
            ColorHexFormat.Argb => (uint)((rgba.B << 0) | (rgba.G << 8) | (rgba.R << 16) | (rgba.A << 24)),
            ColorHexFormat.Rgba => (uint)((rgba.A << 0) | (rgba.B << 8) | (rgba.G << 16) | (rgba.R << 24)),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported color hex format.")
        };

        return hexOrder.ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public override string ToString() => this.ToHex(ColorHexFormat.Rgba);

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

    /// <summary>
    /// Gets the hexadecimal string representation of the color instance in the format RRGGBBAA.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="Rgba32"/> equivalent of the hexadecimal input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool TryParseRgbaHex(string? hex, out Rgba32 result)
    {
        result = default;

        if (!TryConvertToRgbaUInt32(hex, out uint packedValue))
        {
            return false;
        }

        result = Unsafe.As<uint, Rgba32>(ref packedValue);
        return true;
    }

    /// <summary>
    /// Gets the hexadecimal string representation of the color instance in the format AARRGGBB.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="Argb32"/> equivalent of the hexadecimal input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool TryParseArgbHex(string? hex, out Argb32 result)
    {
        result = default;

        if (!TryConvertToArgbUInt32(hex, out uint packedValue))
        {
            return false;
        }

        result = Unsafe.As<uint, Argb32>(ref packedValue);
        return true;
    }

    private static bool TryConvertToRgbaUInt32(string? value, out uint result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> hex = value.AsSpan();

        if (hex[0] == '#')
        {
            hex = hex[1..];
        }

        byte a = 255, r, g, b;

        switch (hex.Length)
        {
            case 8:
                if (!TryParseByte(hex[0], hex[1], out r) ||
                    !TryParseByte(hex[2], hex[3], out g) ||
                    !TryParseByte(hex[4], hex[5], out b) ||
                    !TryParseByte(hex[6], hex[7], out a))
                {
                    return false;
                }

                break;

            case 6:
                if (!TryParseByte(hex[0], hex[1], out r) ||
                    !TryParseByte(hex[2], hex[3], out g) ||
                    !TryParseByte(hex[4], hex[5], out b))
                {
                    return false;
                }

                break;

            case 4:
                if (!TryExpand(hex[0], out r) ||
                    !TryExpand(hex[1], out g) ||
                    !TryExpand(hex[2], out b) ||
                    !TryExpand(hex[3], out a))
                {
                    return false;
                }

                break;

            case 3:
                if (!TryExpand(hex[0], out r) ||
                    !TryExpand(hex[1], out g) ||
                    !TryExpand(hex[2], out b))
                {
                    return false;
                }

                break;

            default:
                return false;
        }

        result = (uint)(r | (g << 8) | (b << 16) | (a << 24)); // RGBA layout
        return true;
    }

    private static bool TryConvertToArgbUInt32(string? value, out uint result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> hex = value.AsSpan();

        if (hex[0] == '#')
        {
            hex = hex[1..];
        }

        byte a = 255, r, g, b;

        switch (hex.Length)
        {
            case 8:
                if (!TryParseByte(hex[0], hex[1], out a) ||
                    !TryParseByte(hex[2], hex[3], out r) ||
                    !TryParseByte(hex[4], hex[5], out g) ||
                    !TryParseByte(hex[6], hex[7], out b))
                {
                    return false;
                }

                break;

            case 6:
                if (!TryParseByte(hex[0], hex[1], out r) ||
                    !TryParseByte(hex[2], hex[3], out g) ||
                    !TryParseByte(hex[4], hex[5], out b))
                {
                    return false;
                }

                break;

            case 4:
                if (!TryExpand(hex[0], out a) ||
                    !TryExpand(hex[1], out r) ||
                    !TryExpand(hex[2], out g) ||
                    !TryExpand(hex[3], out b))
                {
                    return false;
                }

                break;

            case 3:
                if (!TryExpand(hex[0], out r) ||
                    !TryExpand(hex[1], out g) ||
                    !TryExpand(hex[2], out b))
                {
                    return false;
                }

                break;

            default:
                return false;
        }

        result = (uint)((b << 24) | (g << 16) | (r << 8) | a);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseByte(char hi, char lo, out byte value)
    {
        if (TryConvertHexCharToByte(hi, out byte high) && TryConvertHexCharToByte(lo, out byte low))
        {
            value = (byte)((high << 4) | low);
            return true;
        }

        value = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryExpand(char c, out byte value)
    {
        if (TryConvertHexCharToByte(c, out byte nibble))
        {
            value = (byte)((nibble << 4) | nibble);
            return true;
        }

        value = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertHexCharToByte(char c, out byte value)
    {
        if ((uint)(c - '0') <= 9)
        {
            value = (byte)(c - '0');
            return true;
        }

        char lower = (char)(c | 0x20); // Normalize to lowercase

        if ((uint)(lower - 'a') <= 5)
        {
            value = (byte)(lower - 'a' + 10);
            return true;
        }

        value = 0;
        return false;
    }
}
