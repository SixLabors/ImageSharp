// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GothamMatrixFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a Gotham filter to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;

    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Encapsulates methods with which to add a gotham filter to an image.
    /// </summary>
    internal class GothamMatrixFilter : MatrixFilterBase
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for this filter instance.
        /// </summary>
        public override ColorMatrix Matrix
        {
            get { return ColorMatrixes.GreyScale; }
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image.
        /// </returns>
        public override Image TransformImage(Image image, Image newImage)
        {
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(this.Matrix);

                    Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                    graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                    // Overlay the image with some semi-transparent colors to finish the effect.
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddRectangle(rectangle);

                        // Paint a burgundy rectangle with a transparency of ~30% over the image.
                        // Paint a blue rectangle with a transparency of 20% over the image.
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(77, 38, 14, 28)))
                        {
                            Region oldClip = graphics.Clip;
                            graphics.Clip = new Region(rectangle);
                            graphics.FillRectangle(brush, rectangle);

                            // Fill the blue.
                            brush.Color = Color.FromArgb(51, 29, 32, 59);
                            graphics.FillRectangle(brush, rectangle);
                            graphics.Clip = oldClip;
                        }
                    }
                }
            }

            // Add brightness and contrast to finish the effect.
            newImage = Adjustments.Brightness((Bitmap)newImage, 5);
            newImage = Adjustments.Contrast((Bitmap)newImage, 85);

            // Reassign the image.
            image.Dispose();
            image = newImage;

            return image;
        }
    }
}
