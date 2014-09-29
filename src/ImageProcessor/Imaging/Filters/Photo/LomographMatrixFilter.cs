// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LomographMatrixFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a lomograph filter to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Encapsulates methods with which to add a lomograph filter to an image.
    /// </summary>
    internal class LomographMatrixFilter : MatrixFilterBase
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for this filter instance.
        /// </summary>
        public override ColorMatrix Matrix
        {
            get { return ColorMatrixes.Lomograph; }
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
                }
            }

            // Add a vignette to finish the effect.
            newImage = Effects.Vignette((Bitmap)newImage, Color.Black);

            // Reassign the image.
            image.Dispose();
            image = newImage;

            return image;
        }
    }
}
