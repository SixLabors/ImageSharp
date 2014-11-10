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
    using System.Drawing.Drawing2D;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Adds an image overlay to the current image. 
    /// If the overlay is larger than the image it will be scaled to match the image.
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
                Bitmap overlay = new Bitmap(imageLayer.Image);

                // Set the resolution of the overlay and the image to match.
                overlay.SetResolution(newImage.HorizontalResolution, newImage.VerticalResolution);

                Size size = imageLayer.Size;
                int width = image.Width;
                int height = image.Height;
                int overlayWidth = size != Size.Empty ? Math.Min(image.Size.Width, size.Width) : Math.Min(image.Size.Width, overlay.Size.Width);
                int overlayHeight = size != Size.Empty ? Math.Min(image.Size.Height, size.Height) : Math.Min(image.Size.Height, overlay.Size.Height);

                Point? position = imageLayer.Position;
                int opacity = imageLayer.Opacity;

                if (image.Size != overlay.Size || image.Size != new Size(overlayWidth, overlayHeight))
                {
                    // Find the maximum possible dimensions and resize the image.
                    ResizeLayer layer = new ResizeLayer(new Size(overlayWidth, overlayHeight), ResizeMode.Max);
                    overlay = new Resizer(layer).ResizeImage(overlay);
                    overlayWidth = overlay.Width;
                    overlayHeight = overlay.Height;
                }

                // Figure out bounds.
                Rectangle parent = new Rectangle(0, 0, width, height);
                Rectangle child = new Rectangle(0, 0, overlayWidth, overlayHeight);

                // Apply opacity.
                if (opacity < 100)
                {
                    overlay = Adjustments.Alpha(overlay, opacity);
                }

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;

                    if (position != null)
                    {
                        // Draw the image in position catering for overflow.
                        graphics.DrawImage(overlay, new Point(Math.Min(position.Value.X, width - overlayWidth), Math.Min(position.Value.Y, height - overlayHeight)));
                    }
                    else
                    {
                        RectangleF centered = ImageMaths.CenteredRectangle(parent, child);
                        graphics.DrawImage(overlay, new PointF(centered.X, centered.Y));
                    }
                }

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
