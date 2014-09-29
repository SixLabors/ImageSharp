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

    /// <summary>
    /// Provides reusable adjustment methods to apply to images.
    /// </summary>
    public static class Adjustments
    {
        /// <summary>
        /// Adjusts the brightness component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Bitmap"/> source to adjust.
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
        public static Bitmap Brightness(Bitmap source, int threshold, Rectangle? rectangle = null)
        {
            if (threshold > 100 || threshold < -100)
            {
                throw new ArgumentOutOfRangeException("threshold", "Threshold should be between -100 and 100.");
            }

            float brightnessFactor = (float)threshold / 100;
            Rectangle bounds = rectangle.HasValue ? rectangle.Value : new Rectangle(0, 0, source.Width, source.Height);

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

            return source;
        }

        /// <summary>
        /// Adjusts the contrast component of the given image.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Bitmap"/> source to adjust.
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
        public static Bitmap Contrast(Bitmap source, int threshold, Rectangle? rectangle = null)
        {
            if (threshold > 100 || threshold < -100)
            {
                throw new ArgumentOutOfRangeException("threshold", "Threshold should be between -100 and 100.");
            }

            Rectangle bounds = rectangle.HasValue ? rectangle.Value : new Rectangle(0, 0, source.Width, source.Height);

            float contrastFactor = (float)threshold / 100;

            // Stop at -1 to prevent inversion.
            contrastFactor++;
            float factorTransform = 0.5f * (1.0f - contrastFactor);

            ColorMatrix colorMatrix = new ColorMatrix(
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

            return source;
        }
    }
}
