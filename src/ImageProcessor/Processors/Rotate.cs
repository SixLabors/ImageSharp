// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Rotate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to rotate an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using ImageProcessor.Common.Exceptions;

    /// <summary>
    /// Encapsulates methods to rotate an image.
    /// </summary>
    public class Rotate : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rotate"/> class.
        /// </summary>
        public Rotate()
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
                float angle = this.DynamicParameter;

                // Center of the image
                float rotateAtX = Math.Abs(image.Width / 2);
                float rotateAtY = Math.Abs(image.Height / 2);

                // Create a rotated image.
                newImage = this.RotateImage(image, rotateAtX, rotateAtY, angle);

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

        #region Private Methods
        /// <summary>
        /// Rotates an image to the given angle at the given position.
        /// </summary>
        /// <param name="image">The image to rotate</param>
        /// <param name="rotateAtX">The horizontal pixel coordinate at which to rotate the image.</param>
        /// <param name="rotateAtY">The vertical pixel coordinate at which to rotate the image.</param>
        /// <param name="angle">The angle in degrees at which to rotate the image.</param>
        /// <returns>The image rotated to the given angle at the given position.</returns>
        /// <remarks> 
        /// Based on <see href="http://www.codeproject.com/Articles/58815/C-Image-PictureBox-Rotations?msg=4155374#xx4155374xx"/> 
        /// </remarks>
        private Bitmap RotateImage(Image image, float rotateAtX, float rotateAtY, float angle)
        {
            double widthAsDouble = image.Width;
            double heightAsDouble = image.Height;

            double radians = angle * Math.PI / 180d;
            double radiansSin = Math.Sin(radians);
            double radiansCos = Math.Cos(radians);
            double width1 = (heightAsDouble * radiansSin) + (widthAsDouble * radiansCos);
            double height1 = (widthAsDouble * radiansSin) + (heightAsDouble * radiansCos);

            // Find dimensions in the other direction
            radiansSin = Math.Sin(-radians);
            radiansCos = Math.Cos(-radians);
            double width2 = (heightAsDouble * radiansSin) + (widthAsDouble * radiansCos);
            double height2 = (widthAsDouble * radiansSin) + (heightAsDouble * radiansCos);

            // Get the external vertex for the rotation
            int width = Convert.ToInt32(Math.Max(Math.Abs(width1), Math.Abs(width2)));
            int height = Convert.ToInt32(Math.Max(Math.Abs(height1), Math.Abs(height2)));

            int x = (width - image.Width) / 2;
            int y = (height - image.Height) / 2;

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(width, height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                // Put the rotation point in the "center" of the image
                graphics.TranslateTransform(rotateAtX + x, rotateAtY + y);

                // Rotate the image
                graphics.RotateTransform(angle);

                // Move the image back
                graphics.TranslateTransform(-rotateAtX - x, -rotateAtY - y);

                // Draw passed in image onto graphics object
                graphics.DrawImage(image, new PointF(x, y));
            }

            return newImage;
        }
        #endregion
    }
}
