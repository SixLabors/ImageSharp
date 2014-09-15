// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Hue.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to rotate the hue component of an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Encapsulates methods to rotate the hue component of an image.
    /// </summary>
    public class Hue : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Hue"/> class.
        /// </summary>
        public Hue()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the dynamic parameter.
        /// </summary>
        public dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
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
                int degrees = this.DynamicParameter;
                int width = image.Width;
                int height = image.Height;

                newImage = new Bitmap(image);

                using (FastBitmap fastBitmap = new FastBitmap(newImage))
                {
                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            HSLAColor hsl = new HSLAColor(fastBitmap.GetPixel(i, j));
                            hsl.Hue = (hsl.Hue + (degrees / 360f)) % 1;
                            //hsl.Hue = (degrees / 360f);
                            fastBitmap.SetPixel(i, j, hsl);
                        }
                    }
                }

                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }

            return image;
        }
    }
}
