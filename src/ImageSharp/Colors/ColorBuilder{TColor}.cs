// <copyright file="ColorBuilder{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A set of named colors mapped to the provided Color space.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    public static class ColorBuilder<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Creates a new <typeparamref name="TColor"/> representation from the string representing a color.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>Returns a <typeparamref name="TColor"/> that represents the color defined by the provided RGBA heax string.</returns>
        public static TColor FromHex(string hex)
        {
            Guard.NotNullOrEmpty(hex, nameof(hex));

            hex = ToRgbaHex(hex);
            uint packedValue;
            if (hex == null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packedValue))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            TColor result = default(TColor);

            result.PackFromBytes(
                (byte)(packedValue >> 24),
                (byte)(packedValue >> 16),
                (byte)(packedValue >> 8),
                (byte)(packedValue >> 0));
            return result;
        }

        /// <summary>
        /// Creates a new <typeparamref name="TColor"/> representation from standard RGB bytes with 100% opacity.
        /// </summary>
        /// <param name="red">The red intensity.</param>
        /// <param name="green">The green intensity.</param>
        /// <param name="blue">The blue intensity.</param>
        /// <returns>Returns a <typeparamref name="TColor"/> that represents the color defined by the provided RGB values with 100% opacity.</returns>
        public static TColor FromRGB(byte red, byte green, byte blue)
        {
            TColor color = default(TColor);
            color.PackFromBytes(red, green, blue, 255);
            return color;
        }

        /// <summary>
        /// Creates a new <typeparamref name="TColor"/> representation from standard RGBA bytes.
        /// </summary>
        /// <param name="red">The red intensity.</param>
        /// <param name="green">The green intensity.</param>
        /// <param name="blue">The blue intensity.</param>
        /// <param name="alpha">The alpha intensity.</param>
        /// <returns>Returns a <typeparamref name="TColor"/> that represents the color defined by the provided RGBA values.</returns>
        public static TColor FromRGBA(byte red, byte green, byte blue, byte alpha)
        {
            TColor color = default(TColor);
            color.PackFromBytes(red, green, blue, alpha);
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