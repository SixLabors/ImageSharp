// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RotateBounded.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates the methods to rotate an image without expanding the canvas.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Encapsulates the methods to rotate an image without expanding the canvas.
    /// </summary>
    public class RotateBounded : IGraphicsProcessor
    {
        /// <summary>
        /// Gets or sets the DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">The current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class containing
        /// the image to process.</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class.
        /// </returns>
        /// <remarks>
        /// Based on <see href="http://math.stackexchange.com/questions/1070853/"/>
        /// </remarks>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                Tuple<float, bool> rotateParams = this.DynamicParameter;

                // Create a rotated image.
                newImage = this.RotateImage(image, rotateParams.Item1, rotateParams.Item2);

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

        /// <summary>
        /// Rotates the inside of an image to the given angle at the given position.
        /// </summary>
        /// <param name="image">
        /// The image to rotate
        /// </param>
        /// <param name="angleInDegrees">
        /// The angle in degrees.
        /// </param>
        /// <param name="keepSize">
        /// Whether to keep the image dimensions.
        /// <para>
        /// If set to true, the image is zoomed to fit the bounding area.
        /// </para>
        /// <para>
        /// If set to false, the area is cropped to fit the rotated image.
        /// </para>
        /// </param>
        /// <remarks>
        /// Based on the Rotate effect
        /// </remarks>
        /// <returns>
        /// The image rotated to the given angle at the given position.
        /// </returns>
        private Bitmap RotateImage(Image image, float angleInDegrees, bool keepSize)
        {
            Size newSize = new Size(image.Width, image.Height);

            float zoom = ImageMaths.ZoomAfterRotation(image.Width, image.Height, angleInDegrees);

            // if we don't keep the image dimensions, calculate the new ones
            if (!keepSize)
            {
                newSize.Width = (int)Math.Floor(newSize.Width / zoom);
                newSize.Height = (int)Math.Floor(newSize.Height / zoom);
            }

            // Center of the image
            float rotateAtX = Math.Abs(image.Width / 2);
            float rotateAtY = Math.Abs(image.Height / 2);

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(newSize.Width, newSize.Height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                if (keepSize)
                {
                    // Put the rotation point in the "center" of the image
                    graphics.TranslateTransform(rotateAtX, rotateAtY);

                    // Rotate the image
                    graphics.RotateTransform(angleInDegrees);

                    // Zooms the image to fit the area
                    graphics.ScaleTransform(zoom, zoom);

                    // Move the image back
                    graphics.TranslateTransform(-rotateAtX, -rotateAtY);

                    // Draw passed in image onto graphics object
                    graphics.DrawImage(image, new PointF(0, 0));
                }
                else
                {
                    float previousX = rotateAtX;
                    float previousY = rotateAtY;

                    // Calculate the difference between the center of the original image 
                    // and the center of the new image.
                    rotateAtX = Math.Abs(newImage.Width / 2);
                    rotateAtY = Math.Abs(newImage.Height / 2);

                    // Put the rotation point in the "center" of the image
                    graphics.TranslateTransform(rotateAtX, rotateAtY);

                    // Rotate the image
                    graphics.RotateTransform(angleInDegrees);

                    // Draw passed in image onto graphics object
                    graphics.DrawImage(image, new PointF(-previousX, -previousY));
                }
            }

            return newImage;
        }
    }
}