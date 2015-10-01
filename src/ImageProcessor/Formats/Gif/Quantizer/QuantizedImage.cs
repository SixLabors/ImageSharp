// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QuantizedImage.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods for allowing quantization of images pixels.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// Represents a quantized image where the pixels indexed by a color palette.
    /// </summary>
    public class QuantizedImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizedImage"/> class.
        /// </summary>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="palette">The color palette.</param>
        /// <param name="pixels">The quantized pixels.</param>
        public QuantizedImage(int width, int height, Bgra[] palette, byte[] pixels)
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
        public Bgra[] Palette { get; }

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
        public Image ToImage()
        {
            Image image = new Image();

            int pixelCount = this.Pixels.Length;
            byte[] bgraPixels = new byte[pixelCount * 4];

            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                Bgra color = this.Palette[this.Pixels[i]];
                bgraPixels[offset + 0] = color.B;
                bgraPixels[offset + 1] = color.G;
                bgraPixels[offset + 2] = color.R;
                bgraPixels[offset + 3] = color.A;
            }

            image.SetPixels(this.Width, this.Height, bgraPixels);
            return image;
        }
    }
}
