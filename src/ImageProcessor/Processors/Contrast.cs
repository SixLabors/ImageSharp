// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Contrast.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the contrast component of the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// Encapsulates methods to change the contrast component of the image.
    /// </summary>
    public class Contrast : IGraphicsProcessor
    {
        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                float contrastFactor = (float)this.DynamicParameter / 100;

                // Stop at -1 to prevent inversion.
                contrastFactor++;
                float factorTransform = 0.5f * (1.0f - contrastFactor);

                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);

                ColorMatrix colorMatrix = new ColorMatrix(
                                                new[]
                                                        {
                                                            new[] { contrastFactor, 0, 0, 0, 0 }, 
                                                            new[] { 0, contrastFactor, 0, 0, 0 },
                                                            new[] { 0, 0, contrastFactor, 0, 0 },
                                                            new float[] { 0, 0, 0, 1, 0 },
                                                            new[] { factorTransform, factorTransform, factorTransform, 0, 1 }
                                                      });

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (ImageAttributes imageAttributes = new ImageAttributes())
                    {
                        imageAttributes.SetColorMatrix(colorMatrix);

                        graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, imageAttributes);

                        image.Dispose();
                        image = newImage;
                    }
                }
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return image;
        }
    }
}