// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
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
        private readonly Rgba64 data;
        private readonly IPixel boxedHighPrecisionPixel;

        [MethodImpl(InliningOptions.ShortMethod)]
        private Color(byte r, byte g, byte b, byte a)
        {
            this.data = new Rgba64(
                ColorNumerics.UpscaleFrom8BitTo16Bit(r),
                ColorNumerics.UpscaleFrom8BitTo16Bit(g),
                ColorNumerics.UpscaleFrom8BitTo16Bit(b),
                ColorNumerics.UpscaleFrom8BitTo16Bit(a));

            this.boxedHighPrecisionPixel = null;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private Color(byte r, byte g, byte b)
        {
            this.data = new Rgba64(
                ColorNumerics.UpscaleFrom8BitTo16Bit(r),
                ColorNumerics.UpscaleFrom8BitTo16Bit(g),
                ColorNumerics.UpscaleFrom8BitTo16Bit(b),
                ushort.MaxValue);

            this.boxedHighPrecisionPixel = null;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
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
        [MethodImpl(InliningOptions.ShortMethod)]
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Color left, Color right) => !left.Equals(right);

        /// <summary>
        /// Creates a <see cref="Color"/> from RGBA bytes.
        /// </summary>
        /// <param name="r">The red component (0-255).</param>
        /// <param name="g">The green component (0-255).</param>
        /// <param name="b">The blue component (0-255).</param>
        /// <param name="a">The alpha component (0-255).</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

        /// <summary>
        /// Creates a <see cref="Color"/> from RGB bytes.
        /// </summary>
        /// <param name="r">The red component (0-255).</param>
        /// <param name="g">The green component (0-255).</param>
        /// <param name="b">The blue component (0-255).</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);

        /// <summary>
        /// Creates a <see cref="Color"/> from the given <typeparamref name="TPixel"/>.
        /// </summary>
        /// <param name="pixel">The pixel to convert from.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromPixel<TPixel>(TPixel pixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Avoid boxing in case we can convert to Rgba64 safely and efficently
            if (typeof(TPixel) == typeof(Rgba64))
            {
                return new((Rgba64)(object)pixel);
            }
            else if (typeof(TPixel) == typeof(Rgb48))
            {
                return new((Rgb48)(object)pixel);
            }
            else if (typeof(TPixel) == typeof(La32))
            {
                return new((La32)(object)pixel);
            }
            else if (typeof(TPixel) == typeof(L16))
            {
                return new((L16)(object)pixel);
            }
            else if (Unsafe.SizeOf<TPixel>() <= Unsafe.SizeOf<Rgba32>())
            {
                Rgba32 p = default;
                pixel.ToRgba32(ref p);
                return new(p);
            }
            else
            {
                return new(pixel);
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color ParseHex(string hex)
        {
            var rgba = Rgba32.ParseHex(hex);

            return new Color(rgba);
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool TryParseHex(string hex, out Color result)
        {
            result = default;

            if (Rgba32.TryParseHex(hex, out Rgba32 rgba))
            {
                result = new Color(rgba);
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
            var v = (Vector4)this;
            v.W = alpha;
            return new Color(v);
        }

        /// <summary>
        /// Gets the hexadecimal representation of the color instance in rrggbbaa form.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public string ToHex() => this.data.ToRgba32().ToHex();

        /// <inheritdoc />
        public override string ToString() => this.ToHex();

        /// <summary>
        /// Converts the color instance to a specified <typeparamref name="TPixel"/> type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
        /// <returns>The pixel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public TPixel ToPixel<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.boxedHighPrecisionPixel is TPixel pixel)
            {
                return pixel;
            }

            if (this.boxedHighPrecisionPixel is null)
            {
                pixel = default;
                pixel.FromRgba64(this.data);
                return pixel;
            }

            pixel = default;
            pixel.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return pixel;
        }

        /// <summary>
        /// Bulk converts a span of <see cref="Color"/> to a span of a specified <typeparamref name="TPixel"/> type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The source color span.</param>
        /// <param name="destination">The destination pixel span.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToPixel<TPixel>(
            Configuration configuration,
            ReadOnlySpan<Color> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = source[i].ToPixel<TPixel>();
            }
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Color other)
        {
            if (this.boxedHighPrecisionPixel is null && other.boxedHighPrecisionPixel is null)
            {
                return this.data.PackedValue == other.data.PackedValue;
            }

            return this.boxedHighPrecisionPixel?.Equals(other.boxedHighPrecisionPixel) == true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Color other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.PackedValue.GetHashCode();
            }

            return this.boxedHighPrecisionPixel.GetHashCode();
        }
    }
}
