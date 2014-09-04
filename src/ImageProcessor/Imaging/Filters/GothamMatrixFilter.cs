// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GothamMatrixFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a Gotham filter to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    #region Using
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;

    using ImageProcessor.Processors;

    #endregion

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
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public override Image TransformImage(ImageFactory factory, Image image, Image newImage)
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
            factory.Image = newImage;
            Brightness brightness = new Brightness { DynamicParameter = 5 };
            newImage = brightness.ProcessImage(factory);

            factory.Image = newImage;
            Contrast contrast = new Contrast { DynamicParameter = 85 };
            newImage = contrast.ProcessImage(factory);

            // Reassign the image.
            image.Dispose();
            image = newImage;

            return image;
        }
    }
}
