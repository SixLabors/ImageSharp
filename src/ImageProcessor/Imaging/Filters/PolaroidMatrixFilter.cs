// -----------------------------------------------------------------------
// <copyright file="PolaroidMatrixFilter.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Drawing;
    using System.Drawing.Imaging;
    using ImageProcessor.Processors;
    using System.Drawing.Drawing2D;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add a polaroid filter to an image.
    /// </summary>
    class PolaroidMatrixFilter : IMatrixFilter
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColoMatrix"/> for this filter instance.
        /// </summary>
        public ColorMatrix Matrix
        {
            get { return ColorMatrixes.Polaroid; }
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory, Image image, Image newImage)
        {
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(this.Matrix);

                    Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                    graphics.DrawImage(image, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                    // Add a glow to the image.
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(rectangle);
                        using (PathGradientBrush brush = new PathGradientBrush(path))
                        {
                            // Fill a rectangle with an elliptical gradient brush that goes from orange to transparent.
                            // This has the effect of painting the far corners transparent and fading in to orange on the 
                            // way in to the centre.
                            brush.WrapMode = WrapMode.Tile;
                            brush.CenterColor = Color.FromArgb(70, 255, 153, 102);
                            brush.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };

                            Blend blend = new Blend
                            {
                                Positions = new float[] { 0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0F },
                                Factors = new float[] { 0.0f, 0.5f, 1f, 1f, 1.0f, 1.0f }
                            };

                            brush.Blend = blend;

                            Region oldClip = graphics.Clip;
                            graphics.Clip = new Region(rectangle);
                            graphics.FillRectangle(brush, rectangle);
                            graphics.Clip = oldClip;
                        }
                    }
                }
            }

            // Add a vignette to finish the effect.
            factory.Image = newImage;
            Vignette vignette = new Vignette();
            newImage = (Bitmap)vignette.ProcessImage(factory);

            // Reassign the image.
            image.Dispose();
            image = newImage;

            return image;
        }
    }
}
