// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    public readonly partial struct Color : IEquatable<Color>
    {
        private readonly Rgba64 data;

        public Color(Rgba64 pixel)
        {
            this.data = pixel;
        }

        public Color(Rgba32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Argb32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Bgra32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Rgb24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Bgr24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Vector4 vector)
        {
            this.data = new Rgba64(vector);
        }

        public static implicit operator Color(Rgba64 source) => new Color(source);

        public static implicit operator Color(Rgba32 source) => new Color(source);

        public static implicit operator Color(Bgra32 source) => new Color(source);

        public static implicit operator Color(Argb32 source) => new Color(source);

        public static implicit operator Color(Rgb24 source) => new Color(source);

        public static implicit operator Color(Bgr24 source) => new Color(source);

        public static implicit operator Rgba64(Color color) => color.data;

        public static implicit operator Rgba32(Color color) => color.data.ToRgba32();

        public static implicit operator Bgra32(Color color) => color.data.ToBgra32();

        public static implicit operator Argb32(Color color) => color.data.ToArgb32();

        public static implicit operator Rgb24(Color color) => color.data.ToRgb24();

        public static implicit operator Bgr24(Color color) => color.data.ToBgr24();

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        public static Color FromRgba(byte r, byte g, byte b, byte a) => new Color(new Rgba32(r, g, b, a));

        /// <summary>
        /// Creates a new <see cref="Color"/> instance from the string representing a color in hexadecimal form.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>Returns a <see cref="Color"/> that represents the color defined by the provided RGBA hex string.</returns>
        public static Color FromHex(string hex)
        {
            Guard.NotNullOrWhiteSpace(hex, nameof(hex));

            hex = ToRgbaHex(hex);

            if (hex is null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packedValue))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            var rgba = new Rgba32(BinaryPrimitives.ReverseEndianness(packedValue));
            return new Color(rgba);
        }

        /// <summary>
        /// Gets the hexadecimal representation of the color instance in rrggbbaa form.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public string ToHex() => this.data.ToRgba32().ToHex();

        /// <inheritdoc />
        public override string ToString() => this.ToHex();

        /// <summary>
        /// Converts the specified hex value to an rrggbbaa hex value.
        /// </summary>
        /// <param name="hex">The hex value to convert.</param>
        /// <returns>
        /// A rrggbbaa hex value.
        /// </returns>
        private static string ToRgbaHex(string hex)
        {
            if (hex[0] == '#')
            {
                hex = hex.Substring(1);
            }

            if (hex.Length == 8)
            {
                return hex;
            }

            if (hex.Length == 6)
            {
                return hex + "FF";
            }

            if (hex.Length < 3 || hex.Length > 4)
            {
                return null;
            }

            char r = hex[0];
            char g = hex[1];
            char b = hex[2];
            char a = hex.Length == 3 ? 'F' : hex[3];

            return new string(new[] { r, r, g, g, b, b, a, a });
        }

        public TPixel ToPixel<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            pixel.FromRgba64(this.data);
            return pixel;
        }

        public bool Equals(Color other)
        {
            return this.data.PackedValue == other.data.PackedValue;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is Color other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.data.PackedValue.GetHashCode();
        }
    }
}