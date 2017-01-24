// <copyright file="QuantizedImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a quantized image where the pixels indexed by a color palette.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class QuantizedImage<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizedImage{TColor}"/> class.
        /// </summary>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="palette">The color palette.</param>
        /// <param name="pixels">The quantized pixels.</param>
        public QuantizedImage(int width, int height, TColor[] palette, byte[] pixels)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));
            Guard.NotNull(palette, nameof(palette));
            Guard.NotNull(pixels, nameof(pixels));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException($"Pixel array size must be {nameof(width)} * {nameof(height)}", nameof(pixels));
            }

            this.Width = width;
            this.Height = height;
            this.Palette = palette;
            this.Pixels = pixels;
        }

        /// <summary>
        /// Gets the width of this <see cref="T:QuantizedImage"/>.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of this <see cref="T:QuantizedImage"/>.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the color palette of this <see cref="T:QuantizedImage"/>.
        /// </summary>
        public TColor[] Palette { get; }

        /// <summary>
        /// Gets the pixels of this <see cref="T:QuantizedImage"/>.
        /// </summary>
        public byte[] Pixels { get; }
    }
}