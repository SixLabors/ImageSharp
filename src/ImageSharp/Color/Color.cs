// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

        [MethodImpl(InliningOptions.ShortMethod)]
        private Color(byte r, byte g, byte b, byte a)
        {
            this.data = new Rgba64(
                ImageMaths.UpscaleFrom8BitTo16Bit(r),
                ImageMaths.UpscaleFrom8BitTo16Bit(g),
                ImageMaths.UpscaleFrom8BitTo16Bit(b),
                ImageMaths.UpscaleFrom8BitTo16Bit(a));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private Color(byte r, byte g, byte b)
        {
            this.data = new Rgba64(
                ImageMaths.UpscaleFrom8BitTo16Bit(r),
                ImageMaths.UpscaleFrom8BitTo16Bit(g),
                ImageMaths.UpscaleFrom8BitTo16Bit(b),
                ushort.MaxValue);
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
        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks whether two <see cref="Color"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="Color"/> operand.</param>
        /// <param name="right">The right hand <see cref="Color"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a <see cref="Color"/> from RGBA bytes.
        /// </summary>
        /// <param name="r">The red component (0-255).</param>
        /// <param name="g">The green component (0-255).</param>
        /// <param name="b">The blue component (0-255).</param>
        /// <param name="a">The alpha component (0-255).</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromRgba(byte r, byte g, byte b, byte a) => new Color(r, g, b, a);

        /// <summary>
        /// Creates a <see cref="Color"/> from RGB bytes.
        /// </summary>
        /// <param name="r">The red component (0-255).</param>
        /// <param name="g">The green component (0-255).</param>
        /// <param name="b">The blue component (0-255).</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromRgb(byte r, byte g, byte b) => new Color(r, g, b);

        /// <summary>
        /// Creates a new <see cref="Color"/> instance from the string representing a color in hexadecimal form.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>Returns a <see cref="Color"/> that represents the color defined by the provided RGBA hex string.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Color FromHex(string hex)
        {
            Rgba32 rgba = Rgba32.FromHex(hex);

            return new Color(rgba);
        }

        /// <summary>
        /// Alters the alpha channel of the color, returning a new instance.
        /// </summary>
        /// <param name="alpha">The new value of alpha [0..1].</param>
        /// <returns>The color having it's alpha channel altered.</returns>
        public Color WithAlpha(float alpha)
        {
            Vector4 v = (Vector4)this;
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
        /// Converts the color instance to an <see cref="IPixel{TSelf}"/>
        /// implementation defined by <typeparamref name="TPixel"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type to convert to.</typeparam>
        /// <returns>The pixel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public TPixel ToPixel<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            pixel.FromRgba64(this.data);
            return pixel;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Color other)
        {
            return this.data.PackedValue == other.data.PackedValue;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Color other && this.Equals(other);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode()
        {
            return this.data.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Bulk convert a span of <see cref="Color"/> to a span of a specified pixel type.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ToPixel<TPixel>(
            Configuration configuration,
            ReadOnlySpan<Color> source,
            Span<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            ReadOnlySpan<Rgba64> rgba64Span = MemoryMarshal.Cast<Color, Rgba64>(source);
            PixelOperations<TPixel>.Instance.FromRgba64(Configuration.Default, rgba64Span, destination);
        }
    }
}