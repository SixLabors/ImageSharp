// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Alpha.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the alpha component of the image to effect its transparency.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Core.Common.Exceptions;

    /// <summary>
    /// Encapsulates methods to change the alpha component of the image to effect its transparency.
    /// </summary>
    public class Alpha : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Alpha"/> class.
        /// </summary>
        public Alpha()
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
                int alphaPercent = this.DynamicParameter;

                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);

                ColorMatrix colorMatrix = new ColorMatrix();
                colorMatrix.Matrix00 = colorMatrix.Matrix11 = colorMatrix.Matrix22 = colorMatrix.Matrix44 = 1;
                colorMatrix.Matrix33 = (float)alphaPercent / 100;

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
                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }
            finally
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
