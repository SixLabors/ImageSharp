// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Rotate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to rotate the inside of an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates the methods to rotate the inside of an image
    /// </summary>
    public class RotateInside : IGraphicsProcessor
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
                RotateInsideLayer rotateLayer = this.DynamicParameter;

                // Create a rotated image.
                newImage = this.RotateImage(image, rotateLayer);

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
        /// <param name="image">The image to rotate</param>
        /// <param name="rotateLayer">The rotation layer.</param>
        /// <remarks>
        /// Based on the Rotate effect
        /// </remarks>
        /// <returns>The image rotated to the given angle at the given position.</returns>
        private Bitmap RotateImage(Image image, RotateInsideLayer rotateLayer)
        {
            Size newSize = new Size(image.Width, image.Height);

            float zoom = Imaging.Rotation.ZoomAfterRotation(image.Width, image.Height, rotateLayer.Angle);

            // if we don't keep the image dimensions, calculate the new ones
            if (!rotateLayer.KeepImageDimensions)
            {
                newSize.Width = (int)(newSize.Width / zoom);
                newSize.Height = (int)(newSize.Height / zoom);
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

                if (rotateLayer.KeepImageDimensions)
                {
                    // Put the rotation point in the "center" of the image
                    graphics.TranslateTransform(rotateAtX, rotateAtY);

                    // Rotate the image
                    graphics.RotateTransform(rotateLayer.Angle);

                    // Zooms the image to fit the area
                    graphics.ScaleTransform(zoom, zoom);

                    // Move the image back
                    graphics.TranslateTransform(-rotateAtX, -rotateAtY);

                    // Draw passed in image onto graphics object
                    graphics.DrawImage(image, new PointF(0, 0));
                }
                else
                {
                    // calculate the difference between the center of the original image and the center of the new image
                    int diffX = (image.Width - newSize.Width) / 2;
                    int diffY = (image.Height - newSize.Height) / 2;

                    // Put the rotation point in the "center" of the old image
                    graphics.TranslateTransform(rotateAtX - diffX, rotateAtY - diffY);

                    // Rotate the image
                    graphics.RotateTransform(rotateLayer.Angle);

                    // Move the image back
                    graphics.TranslateTransform(-(rotateAtX - diffX), -(rotateAtY - diffY));

                    // Draw passed in image onto graphics object
                    graphics.DrawImage(image, new PointF(-diffX, -diffY));
                }
            }

            return newImage;
        }
    }
}