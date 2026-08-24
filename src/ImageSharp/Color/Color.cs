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
    private readonly bool isAssociated;
    private readonly bool dataIsAssociated;

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="vector">The <see cref="Vector4"/> containing the color information.</param>
    /// <param name="alphaRepresentation">The alpha representation exposed by the color.</param>
    /// <param name="dataAlphaRepresentation">The alpha representation of <paramref name="vector"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color(Vector4 vector, PixelAlphaRepresentation alphaRepresentation, PixelAlphaRepresentation dataAlphaRepresentation)
    {
        this.data = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
        this.boxedHighPrecisionPixel = null;
        this.isAssociated = alphaRepresentation == PixelAlphaRepresentation.Associated;
        this.dataIsAssociated = dataAlphaRepresentation == PixelAlphaRepresentation.Associated;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Color"/> struct.
    /// </summary>
    /// <param name="pixel">The pixel containing color information.</param>
    /// <param name="alphaRepresentation">The alpha representation of <paramref name="pixel"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color(IPixel pixel, PixelAlphaRepresentation alphaRepresentation)
    {
        this.boxedHighPrecisionPixel = pixel;
        this.data = default;
        this.isAssociated = alphaRepresentation == PixelAlphaRepresentation.Associated;
        this.dataIsAssociated = this.isAssociated;
    }

    /// <summary>
    /// Gets the alpha representation used by this color's scaled vector.
    /// </summary>
    public PixelAlphaRepresentation AlphaRepresentation
        => this.isAssociated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated;

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

        if (info.ComponentInfo.HasValue)
        {
            int maximumComponentPrecision = info.ComponentInfo.Value.GetMaximumComponentPrecision();

            if (maximumComponentPrecision <= (int)PixelComponentBitDepth.Bit32)
            {
                if (info.AlphaRepresentation == PixelAlphaRepresentation.Associated && maximumComponentPrecision <= (int)PixelComponentBitDepth.Bit8)
                {
                    // Associated formats with at most eight bits per component can be canonicalized without loss by their pixel-specific conversion.
                    // Higher-precision formats retain their associated values because unassociation can lose representable data.
                    Vector4 vector = source.ToUnassociatedScaledVector4();
                    return new Color(vector, info.AlphaRepresentation, PixelAlphaRepresentation.Unassociated);
                }

                return new Color(source.ToScaledVector4(), info.AlphaRepresentation, info.AlphaRepresentation);
            }
        }

        return new Color(source, info.AlphaRepresentation);
    }

    /// <summary>
    /// Creates a <see cref="Color"/> from a generic scaled <see cref="Vector4"/>.
    /// </summary>
    /// <param name="source">The unassociated vector to load the color from.</param>
    /// <returns>The <see cref="Color"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color FromScaledVector(Vector4 source)
        => new(source, PixelAlphaRepresentation.Unassociated, PixelAlphaRepresentation.Unassociated);

    /// <summary>
    /// Creates a <see cref="Color"/> from a generic scaled <see cref="Vector4"/> with the specified alpha representation.
    /// </summary>
    /// <param name="source">The vector to load the color from.</param>
    /// <param name="alphaRepresentation">The alpha representation of <paramref name="source"/>.</param>
    /// <returns>The <see cref="Color"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color FromScaledVector(Vector4 source, PixelAlphaRepresentation alphaRepresentation)
        => new(source, alphaRepresentation, alphaRepresentation);

    /// <summary>
    /// Bulk converts a span of generic scaled <see cref="Vector4"/> to a span of <see cref="Color"/>.
    /// </summary>
    /// <param name="source">The source vector span.</param>
    /// <param name="destination">The destination color span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromScaledVector(ReadOnlySpan<Vector4> source, Span<Color> destination)
        => FromScaledVector(source, destination, PixelAlphaRepresentation.Unassociated);

    /// <summary>
    /// Bulk converts a span of generic scaled <see cref="Vector4"/> values with the specified alpha representation
    /// to a span of <see cref="Color"/> values.
    /// </summary>
    /// <param name="source">The source vector span.</param>
    /// <param name="destination">The destination color span.</param>
    /// <param name="alphaRepresentation">The alpha representation of the source vectors.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromScaledVector(ReadOnlySpan<Vector4> source, Span<Color> destination, PixelAlphaRepresentation alphaRepresentation)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector(source[i], alphaRepresentation);
        }
    }

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

        if (info.ComponentInfo.HasValue)
        {
            int maximumComponentPrecision = info.ComponentInfo.Value.GetMaximumComponentPrecision();

            if (maximumComponentPrecision <= (int)PixelComponentBitDepth.Bit32)
            {
                if (info.AlphaRepresentation == PixelAlphaRepresentation.Associated && maximumComponentPrecision <= (int)PixelComponentBitDepth.Bit8)
                {
                    // Match the scalar conversion by retaining exact unassociated values from the format-specific operation.
                    for (int i = 0; i < source.Length; i++)
                    {
                        Vector4 vector = source[i].ToUnassociatedScaledVector4();
                        destination[i] = new Color(vector, info.AlphaRepresentation, PixelAlphaRepresentation.Unassociated);
                    }

                    return;
                }

                for (int i = 0; i < source.Length; i++)
                {
                    destination[i] = new Color(source[i].ToScaledVector4(), info.AlphaRepresentation, info.AlphaRepresentation);
                }

                return;
            }
        }

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = new Color(source[i], info.AlphaRepresentation);
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
    /// <returns>The color having its alpha channel altered.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color WithAlpha(float alpha)
    {
        Vector4 vector = this.ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        vector.W = Numerics.Clamp(alpha, 0, 1);
        return new Color(vector, this.AlphaRepresentation, PixelAlphaRepresentation.Unassociated);
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
        Rgba32 rgba = this.ToPixel<Rgba32>();

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

        Vector4 vector = this.boxedHighPrecisionPixel?.ToScaledVector4() ?? this.data;
        if (this.dataIsAssociated)
        {
            // Preserve associated components directly while allowing the destination to quantize alpha to its own storage grid.
            return TPixel.FromAssociatedScaledVector4(vector);
        }

        // Unassociated input lets an associated destination quantize alpha before it multiplies the color components.
        return TPixel.FromUnassociatedScaledVector4(vector);
    }

    /// <summary>
    /// Expands the color into a generic ("scaled") <see cref="Vector4"/> representation,
    /// preserving the <see cref="AlphaRepresentation"/>, with values scaled and clamped between
    /// <value>0</value> and <value>1</value>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToScaledVector4()
    {
        Vector4 vector = this.boxedHighPrecisionPixel?.ToScaledVector4() ?? this.data;

        if (this.dataIsAssociated == this.isAssociated)
        {
            return vector;
        }

        if (this.isAssociated)
        {
            Numerics.Premultiply(ref vector);
        }
        else
        {
            Numerics.UnPremultiply(ref vector);
        }

        return vector;
    }

    /// <summary>
    /// Expands the color into a generic ("scaled") <see cref="Vector4"/> using the specified alpha representation,
    /// with values scaled and clamped between <value>0</value> and <value>1</value>.
    /// </summary>
    /// <param name="alphaRepresentation">
    /// The alpha representation to apply. <see cref="PixelAlphaRepresentation.Associated"/> returns color components
    /// multiplied by alpha; other representations return color components independent of alpha.
    /// </param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToScaledVector4(PixelAlphaRepresentation alphaRepresentation)
    {
        bool targetIsAssociated = alphaRepresentation == PixelAlphaRepresentation.Associated;
        Vector4 vector = this.boxedHighPrecisionPixel?.ToScaledVector4() ?? this.data;

        if (this.dataIsAssociated == targetIsAssociated)
        {
            return vector;
        }

        if (targetIsAssociated)
        {
            Numerics.Premultiply(ref vector);
        }
        else
        {
            Numerics.UnPremultiply(ref vector);
        }

        return vector;
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
            return this.isAssociated == other.isAssociated && this.ToScaledVector4() == other.ToScaledVector4();
        }

        return this.isAssociated == other.isAssociated
            && this.boxedHighPrecisionPixel?.Equals(other.boxedHighPrecisionPixel) == true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && this.Equals(other);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (this.boxedHighPrecisionPixel is null)
        {
            return HashCode.Combine(this.ToScaledVector4(), this.isAssociated);
        }

        return HashCode.Combine(this.boxedHighPrecisionPixel.ToScaledVector4(), this.isAssociated);
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
