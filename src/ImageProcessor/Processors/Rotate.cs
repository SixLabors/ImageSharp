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
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    #endregion

    /// <summary>
    /// Encapsulates methods to rotate an image.
    /// </summary>
    public class Rotate : IGraphicsProcessor
    {
        #region IGraphicsProcessor members
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
                int angle = this.DynamicParameter;

                // Center of the image
                float rotateAtX = Math.Abs(image.Width / 2);
                float rotateAtY = Math.Abs(image.Height / 2);

                // Create a rotated image.
                newImage = this.RotateImage(image, rotateAtX, rotateAtY, angle);

                image.Dispose();
                image = newImage;
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
        #endregion

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
        /// Based on <see cref="http://www.codeproject.com/Articles/58815/C-Image-PictureBox-Rotations?msg=4155374#xx4155374xx"/> 
        /// </remarks>
        private Bitmap RotateImage(Image image, float rotateAtX, float rotateAtY, float angle)
        {
            int width, height, x, y;

            // Degrees to radians according to Google. 
            const double DegreeToRadian = 0.0174532925;

            double widthAsDouble = image.Width;
            double heightAsDouble = image.Height;

            // Allow for angles over 180
            if (angle > 180)
            {
                angle = angle - 360;
            }

            double degrees = Math.Abs(angle);

            if (degrees <= 90)
            {
                double radians = DegreeToRadian * degrees;
                double radiansSin = Math.Sin(radians);
                double radiansCos = Math.Cos(radians);
                width = (int)((heightAsDouble * radiansSin) + (widthAsDouble * radiansCos));
                height = (int)((widthAsDouble * radiansSin) + (heightAsDouble * radiansCos));
                x = (width - image.Width) / 2;
                y = (height - image.Height) / 2;
            }
            else
            {
                degrees -= 90;
                double radians = DegreeToRadian * degrees;
                double radiansSin = Math.Sin(radians);
                double radiansCos = Math.Cos(radians);

                // Fix the 270 error
                if (Math.Abs(radiansCos - -1.0D) < 0.00001)
                {
                    radiansCos = 1;
                }

                width = (int)((widthAsDouble * radiansSin) + (heightAsDouble * radiansCos));
                height = (int)((heightAsDouble * radiansSin) + (widthAsDouble * radiansCos));
                x = (width - image.Width) / 2;
                y = (height - image.Height) / 2;
            }

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(width, height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.HighQuality;
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
                graphics.DrawImage(image, new PointF(0 + x, 0 + y));
            }

            return newImage;
        }
        #endregion
    }
}
