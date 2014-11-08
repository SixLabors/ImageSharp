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
        /// <param name="source">The current image to process</param>
        /// <param name="destination">The new Image to return</param>
        /// <returns>
        /// The processed <see cref="System.Drawing.Bitmap"/>.
        /// </returns>
        public override Bitmap TransformImage(Image source, Image destination)
        {
            using (Graphics graphics = Graphics.FromImage(destination))
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(this.Matrix);

                    Rectangle rectangle = new Rectangle(0, 0, source.Width, source.Height);
                    graphics.DrawImage(source, rectangle, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                }
            }

            // Add a vignette to finish the effect.
            destination = Effects.Vignette(destination, Color.FromArgb(220, 0, 10, 0));

            return (Bitmap)destination;
        }
    }
}
