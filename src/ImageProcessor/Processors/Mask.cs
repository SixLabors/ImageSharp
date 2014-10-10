// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Mask.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Applies a mask to the given image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Applies a mask to the given image. If the mask is not the same size as the image 
    /// it will be centered against the image.
    /// </summary>
    public class Mask : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mask"/> class.
        /// </summary>
        public Mask()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the dynamic parameter.
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
            Bitmap mask = null;
            Bitmap maskResized = null;
            Image image = factory.Image;

            try
            {
                int width = image.Width;
                int height = image.Height;
                mask = new Bitmap(this.DynamicParameter);
                Rectangle parent = new Rectangle(0, 0, width, height);
                Rectangle child = new Rectangle(0, 0, mask.Width, mask.Height);
                RectangleF centered = ImageMaths.CenteredRectangle(parent, child);

                // Resize the mask to the size of the input image so that we can apply it.
                maskResized = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(maskResized))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(mask, new PointF(centered.X, centered.Y));
                }

                newImage = Effects.ApplyMask(image, maskResized);

                mask.Dispose();
                maskResized.Dispose();
                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (mask != null)
                {
                    mask.Dispose();
                }

                if (maskResized != null)
                {
                    maskResized.Dispose();
                }

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
