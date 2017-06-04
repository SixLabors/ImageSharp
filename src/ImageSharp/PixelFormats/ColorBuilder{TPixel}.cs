// <copyright file="ColorBuilder{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A set of named colors mapped to the provided Color space.
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public static class ColorBuilder<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Creates a new <typeparamref name="TPixel"/> representation from the string representing a color.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>Returns a <typeparamref name="TPixel"/> that represents the color defined by the provided RGBA heax string.</returns>
        public static TPixel FromHex(string hex)
        {
            Guard.NotNullOrEmpty(hex, nameof(hex));

            hex = ToRgbaHex(hex);
            uint packedValue;
            if (hex == null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packedValue))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            TPixel result = default(TPixel);
            Rgba32 rgba = new Rgba32(
                (byte)(packedValue >> 24),
                (byte)(packedValue >> 16),
                (byte)(packedValue >> 8),
                (byte)(packedValue >> 0));

            result.PackFromRgba32(rgba);
            return result;
        }

        /// <summary>
        /// Creates a new <typeparamref name="TPixel"/> representation from standard RGB bytes with 100% opacity.
        /// </summary>
        /// <param name="red">The red intensity.</param>
        /// <param name="green">The green intensity.</param>
        /// <param name="blue">The blue intensity.</param>
        /// <returns>Returns a <typeparamref name="TPixel"/> that represents the color defined by the provided RGB values with 100% opacity.</returns>
        public static TPixel FromRGB(byte red, byte green, byte blue) => FromRGBA(red, green, blue, 255);

        /// <summary>
        /// Creates a new <typeparamref name="TPixel"/> representation from standard RGBA bytes.
        /// </summary>
        /// <param name="red">The red intensity.</param>
        /// <param name="green">The green intensity.</param>
        /// <param name="blue">The blue intensity.</param>
        /// <param name="alpha">The alpha intensity.</param>
        /// <returns>Returns a <typeparamref name="TPixel"/> that represents the color defined by the provided RGBA values.</returns>
        public static TPixel FromRGBA(byte red, byte green, byte blue, byte alpha)
        {
            TPixel color = default(TPixel);
            color.PackFromRgba32(new Rgba32(red, green, blue, alpha));
            return color;
        }

        /// <summary>
        /// Converts the specified hex value to an rrggbbaa hex value.
        /// </summary>
        /// <param name="hex">The hex value to convert.</param>
        /// <returns>
        /// A rrggbbaa hex value.
        /// </returns>
        private static string ToRgbaHex(string hex)
        {
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;

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

            string red = char.ToString(hex[0]);
            string green = char.ToString(hex[1]);
            string blue = char.ToString(hex[2]);
            string alpha = hex.Length == 3 ? "F" : char.ToString(hex[3]);

            return string.Concat(red, red, green, green, blue, blue, alpha, alpha);
        }
    }
}