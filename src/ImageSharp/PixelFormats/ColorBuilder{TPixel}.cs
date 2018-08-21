// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Globalization;

namespace SixLabors.ImageSharp.PixelFormats
{
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
            Guard.NotNullOrWhiteSpace(hex, nameof(hex));

            hex = ToRgbaHex(hex);

            if (hex is null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packedValue))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            TPixel result = default;
            var rgba = new Rgba32(BinaryPrimitives.ReverseEndianness(packedValue));

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
            TPixel color = default;
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
    }
}