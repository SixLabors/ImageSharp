// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Adjustments.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides reusable adjustment methods to apply to images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Helpers
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Provides reusable adjustment methods to apply to images.
    /// </summary>
    public static class Adjustments
    {
        /// <summary>
        /// Adjusts the alpha component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Image"/> source to adjust.
        /// </param>
        /// <param name="percentage">
        /// The percentage value between 0 and 100 for adjusting the opacity.
        /// </param>
        /// <param name="rectangle">The rectangle to define the bounds of the area to adjust the opacity. 
        /// If null then the effect is applied to the entire image.</param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the alpha component adjusted.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the percentage value falls outside the acceptable range.
        /// </exception>
        public static Bitmap Alpha(Image source, int percentage, Rectangle? rectangle = null)
        {
            if (percentage > 100 || percentage < 0)
            {
                throw new ArgumentOutOfRangeException("percentage", "Percentage should be between 0 and 100.");
            }

            float factor = (float)percentage / 100;
            int width = source.Width;
            int height = source.Height;

            // Traditional examples using a color matrix alter the rgb values also.
            using (FastBitmap bitmap = new FastBitmap(source))
            {
                // Loop through the pixels.
                Parallel.For(
                    0,
                    height,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // ReSharper disable AccessToDisposedClosure
                            Color color = bitmap.GetPixel(x, y);
                            bitmap.SetPixel(x, y, Color.FromArgb(Convert.ToInt32(color.A * factor), color.R, color.G, color.B));
                            // ReSharper restore AccessToDisposedClosure
                        }
                    });
            }

            return (Bitmap)source;
        }

        /// <summary>
        /// Adjusts the brightness component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Image"/> source to adjust.
        /// </param>
        /// <param name="threshold">
        /// The threshold value between -100 and 100 for adjusting the brightness.
        /// </param>
        /// <param name="rectangle">The rectangle to define the bounds of the area to adjust the brightness. 
        /// If null then the effect is applied to the entire image.</param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the brightness adjusted.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the threshold value falls outside the acceptable range.
        /// </exception>
        public static Bitmap Brightness(Image source, int threshold, Rectangle? rectangle = null)
        {
            if (threshold > 100 || threshold < -100)
            {
                throw new ArgumentOutOfRangeException("threshold", "Threshold should be between -100 and 100.");
            }

            float brightnessFactor = (float)threshold / 100;
            Rectangle bounds = rectangle ?? new Rectangle(0, 0, source.Width, source.Height);

            ColorMatrix colorMatrix =
                new ColorMatrix(
                    new[]
                        {
                            new float[] { 1, 0, 0, 0, 0 }, 
                            new float[] { 0, 1, 0, 0, 0 }, 
                            new float[] { 0, 0, 1, 0, 0 },
                            new float[] { 0, 0, 0, 1, 0 },
                            new[] { brightnessFactor, brightnessFactor, brightnessFactor, 0, 1 }
                        });

            using (Graphics graphics = Graphics.FromImage(source))
            {
                using (ImageAttributes imageAttributes = new ImageAttributes())
                {
                    imageAttributes.SetColorMatrix(colorMatrix);
                    graphics.DrawImage(source, bounds, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, imageAttributes);
                }
            }

            return (Bitmap)source;
        }

        /// <summary>
        /// Adjusts the contrast component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Image"/> source to adjust.
        /// </param>
        /// <param name="threshold">
        /// The threshold value between -100 and 100 for adjusting the contrast.
        /// </param>
        /// <param name="rectangle">The rectangle to define the bounds of the area to adjust the contrast. 
        /// If null then the effect is applied to the entire image.</param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the contrast adjusted.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the threshold value falls outside the acceptable range.
        /// </exception>
        public static Bitmap Contrast(Image source, int threshold, Rectangle? rectangle = null)
        {
            if (threshold > 100 || threshold < -100)
            {
                throw new ArgumentOutOfRangeException("threshold", "Threshold should be between -100 and 100.");
            }

            Rectangle bounds = rectangle ?? new Rectangle(0, 0, source.Width, source.Height);

            float contrastFactor = (float)threshold / 100;

            // Stop at -1 to prevent inversion.
            contrastFactor++;
            float factorTransform = 0.5f * (1.0f - contrastFactor);

            ColorMatrix colorMatrix =
                new ColorMatrix(
                    new[]
                    {
                        new[] { contrastFactor, 0, 0, 0, 0 }, 
                        new[] { 0, contrastFactor, 0, 0, 0 },
                        new[] { 0, 0, contrastFactor, 0, 0 },
                        new float[] { 0, 0, 0, 1, 0 },
                        new[] { factorTransform, factorTransform, factorTransform, 0, 1 }
                    });

            using (Graphics graphics = Graphics.FromImage(source))
            {
                using (ImageAttributes imageAttributes = new ImageAttributes())
                {
                    imageAttributes.SetColorMatrix(colorMatrix);
                    graphics.DrawImage(source, bounds, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, imageAttributes);
                }
            }

            return (Bitmap)source;
        }

        /// <summary>
        /// Adjust the gamma (intensity of the light) component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Image"/> source to adjust.
        /// </param>
        /// <param name="value">
        /// The value to adjust the gamma by (typically between .2 and 5).
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the gamma adjusted.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the value falls outside the acceptable range.
        /// </exception>
        public static Bitmap Gamma(Image source, float value)
        {
            if (value > 5 || value < .1)
            {
                throw new ArgumentOutOfRangeException("value", "Value should be between .1 and 5.");
            }

            int width = source.Width;
            int height = source.Height;
            Bitmap destination = new Bitmap(width, height);
            destination.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            Rectangle rectangle = new Rectangle(0, 0, width, height);
            using (Graphics graphics = Graphics.FromImage(destination))
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetGamma(value);
                    graphics.DrawImage(source, rectangle, 0, 0, width, height, GraphicsUnit.Pixel, attributes);
                }
            }

            source.Dispose();
            return destination;
        }
    }
}
