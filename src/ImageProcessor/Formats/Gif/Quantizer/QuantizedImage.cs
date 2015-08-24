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
        /// Initializes a new instance of <see cref="T:QuantizedImage"/>.
        /// </summary>
        public QuantizedImage(int width, int height, Bgra[] palette, byte[] pixels)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height) throw new ArgumentException("Pixel array size must be width * height", nameof(pixels));

            this.Width = width;
            this.Height = height;
            this.Palette = palette;
            this.Pixels = pixels;
        }

        /// <summary>
        /// Converts this quantized image to a normal image.
        /// </summary>
        /// <returns></returns>
        public Image ToImage()
        {
            // TODO: Something is going wrong here. We have a palette.
            Image image = new Image();
            int pixelCount = Pixels.Length;
            byte[] bgraPixels = new byte[pixelCount * 4];

            for (int i = 0; i < pixelCount; i += 4)
            {
                Bgra color = Palette[Pixels[i]];
                bgraPixels[i + 0] = color.B;
                bgraPixels[i + 1] = color.G;
                bgraPixels[i + 2] = color.R;
                bgraPixels[i + 3] = color.A;
            }

            image.SetPixels(Width, Height, bgraPixels);
            return image;
        }
    }
}
