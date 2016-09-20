// <copyright file="QuantizedImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a quantized image where the pixels indexed by a color palette.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class QuantizedImage<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizedImage{TColor, TPacked}"/> class.
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
                throw new ArgumentException(
                    $"Pixel array size must be {nameof(width)} * {nameof(height)}", nameof(pixels));
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

        /// <summary>
        /// Converts this quantized image to a normal image.
        /// </summary>
        /// <returns>
        /// The <see cref="Image"/>
        /// </returns>
        public Image<TColor, TPacked> ToImage()
        {
            Image<TColor, TPacked> image = new Image<TColor, TPacked>();

            int pixelCount = this.Pixels.Length;
            int palletCount = this.Palette.Length - 1;
            TColor[] pixels = new TColor[pixelCount];

            Parallel.For(
                0,
                pixelCount,
                Bootstrapper.Instance.ParallelOptions,
                i =>
                    {
                        TColor color = this.Palette[Math.Min(palletCount, this.Pixels[i])];
                        pixels[i] = color;
                    });

            image.SetPixels(this.Width, this.Height, pixels);
            return image;
        }
    }
}