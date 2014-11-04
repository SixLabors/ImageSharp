// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Overlay.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Adds an image overlay to the current image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Adds an image overlay to the current image.
    /// </summary>
    public class Overlay : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Overlay"/> class.
        /// </summary>
        public Overlay()
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
            Image image = factory.Image;

            try
            {
                newImage = new Bitmap(image);
                ImageLayer imageLayer = this.DynamicParameter;
                Image overlay = imageLayer.Image;
                Size size = imageLayer.Size;
                int opacity = Math.Min((int)Math.Ceiling((imageLayer.Opacity / 100f) * 255), 255);



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
