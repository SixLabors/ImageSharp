// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Saturation.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the saturation component of the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Common.Exceptions;

    /// <summary>
    /// Encapsulates methods to change the saturation component of the image.
    /// </summary>
    /// <remarks>
    /// <see href="http://www.bobpowell.net/imagesaturation.htm"/> 
    /// </remarks>
    public class Saturation : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Saturation"/> class.
        /// </summary>
        public Saturation()
        {
            this.Settings = new Dictionary<string, string>();
        }

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
                float saturationFactor = (float)this.DynamicParameter / 100;

                // Stop at -1 to prevent inversion.
                saturationFactor++;

                // The matrix is set up to "shear" the colour space using the following set of values.  
                // Note that each colour component has an effective luminance which contributes to the
                // overall brightness of the pixel.
                float saturationComplement = 1.0f - saturationFactor;
                float saturationComplementR = 0.3086f * saturationComplement;
                float saturationComplementG = 0.6094f * saturationComplement;
                float saturationComplementB = 0.0820f * saturationComplement;

                newImage = new Bitmap(image.Width, image.Height);
                newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                ColorMatrix colorMatrix =
                    new ColorMatrix(
                        new[]
                            {
                                new[]
                                    {
                                        saturationComplementR + saturationFactor, saturationComplementR,
                                        saturationComplementR, 0, 0
                                    },
                                new[]
                                    {
                                        saturationComplementG, saturationComplementG + saturationFactor,
                                        saturationComplementG, 0, 0
                                    },
                                new[]
                                    {
                                        saturationComplementB, saturationComplementB,
                                        saturationComplementB + saturationFactor, 0, 0
                                    },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (ImageAttributes imageAttributes = new ImageAttributes())
                    {
                        imageAttributes.SetColorMatrix(colorMatrix);

                        graphics.DrawImage(
                            image,
                            new Rectangle(0, 0, image.Width, image.Height),
                            0,
                            0,
                            image.Width,
                            image.Height,
                            GraphicsUnit.Pixel,
                            imageAttributes);

                        image.Dispose();
                        image = newImage;
                    }
                }
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
