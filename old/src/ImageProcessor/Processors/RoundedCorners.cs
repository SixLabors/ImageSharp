// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RoundedCorners.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to add rounded corners to an image.
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
    /// Encapsulates methods to add rounded corners to an image.
    /// </summary>
    public class RoundedCorners : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoundedCorners"/> class.
        /// </summary>
        public RoundedCorners()
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
                RoundedCornerLayer roundedCornerLayer = this.DynamicParameter;
                int radius = roundedCornerLayer.Radius;
                bool topLeft = roundedCornerLayer.TopLeft;
                bool topRight = roundedCornerLayer.TopRight;
                bool bottomLeft = roundedCornerLayer.BottomLeft;
                bool bottomRight = roundedCornerLayer.BottomRight;

                // Create a rounded image.
                newImage = this.RoundCornerImage(image, radius, topLeft, topRight, bottomLeft, bottomRight);

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
        /// Adds rounded corners to the image
        /// </summary>
        /// <param name="image">The image to add corners too</param>
        /// <param name="cornerRadius">The radius of the corners.</param>
        /// <param name="topLeft">If the top left corner will have a rounded corner?</param>
        /// <param name="topRight">If the top right corner will have a rounded corner?</param>
        /// <param name="bottomLeft">If the bottom left corner will have a rounded corner?</param>
        /// <param name="bottomRight">If the bottom right corner will have a rounded corner?</param>
        /// <returns>The image with rounded corners.</returns>
        private Bitmap RoundCornerImage(Image image, int cornerRadius, bool topLeft = false, bool topRight = false, bool bottomLeft = false, bool bottomRight = false)
        {
            int width = image.Width;
            int height = image.Height;
            int cornerDiameter = cornerRadius * 2;

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(image.Width, image.Height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                // Add rounded corners
                using (GraphicsPath path = new GraphicsPath())
                {
                    // Determined if the top left has a rounded corner
                    if (topLeft)
                    {
                        path.AddArc(0, 0, cornerDiameter, cornerDiameter, 180, 90);
                    }
                    else
                    {
                        path.AddLine(0, 0, 0, 0);
                    }

                    // Determined if the top right has a rounded corner
                    if (topRight)
                    {
                        path.AddArc(0 + width - cornerDiameter, 0, cornerDiameter, cornerDiameter, 270, 90);
                    }
                    else
                    {
                        path.AddLine(width, 0, width, 0);
                    }

                    // Determined if the bottom left has a rounded corner
                    if (bottomRight)
                    {
                        path.AddArc(0 + width - cornerDiameter, 0 + height - cornerDiameter, cornerDiameter, cornerDiameter, 0, 90);
                    }
                    else
                    {
                        path.AddLine(width, height, width, height);
                    }

                    // Determined if the bottom right has a rounded corner
                    if (bottomLeft)
                    {
                        path.AddArc(0, 0 + height - cornerDiameter, cornerDiameter, cornerDiameter, 90, 90);
                    }
                    else
                    {
                        path.AddLine(0, height, 0, height);
                    }

                    using (Brush brush = new TextureBrush(image))
                    {
                        graphics.FillPath(brush, path);
                    }
                }
            }

            return newImage;
        }
    }
}
