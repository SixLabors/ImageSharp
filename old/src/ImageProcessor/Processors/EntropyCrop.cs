// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EntropyCrop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Filters.Binarization;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Performs a crop on an image to the area of greatest entropy.
    /// </summary>
    public class EntropyCrop : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCrop"/> class.
        /// </summary>
        public EntropyCrop()
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
            Bitmap grey = null;
            Image image = factory.Image;
            byte threshold = this.DynamicParameter;

            try
            {
                // Detect the edges then strip out middle shades.
                grey = new ConvolutionFilter(new SobelEdgeFilter(), true).Process2DFilter(image);
                grey = new BinaryThreshold(threshold).ProcessFilter(grey);

                // Search for the first white pixels
                Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(grey, 0);
                grey.Dispose();

                newImage = new Bitmap(rectangle.Width, rectangle.Height);
                newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    graphics.DrawImage(
                                     image,
                                     new Rectangle(0, 0, rectangle.Width, rectangle.Height),
                                     rectangle.X,
                                     rectangle.Y,
                                     rectangle.Width,
                                     rectangle.Height,
                                     GraphicsUnit.Pixel);
                }

                // Reassign the image.
                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (grey != null)
                {
                    grey.Dispose();
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