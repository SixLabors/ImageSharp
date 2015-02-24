// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Halftone.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The halftone processor applies a classical CMYK halftone to the given image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Filters.Artistic;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// The halftone processor applies a classical CMYK halftone to the given image.
    /// </summary>
    public class Halftone : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Halftone"/> class.
        /// </summary>
        public Halftone()
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
            Image image = factory.Image;
            int width = image.Width;
            int height = image.Height;
            Bitmap newImage = null;
            Bitmap edgeBitmap = null;
            try
            {
                HalftoneFilter filter = new HalftoneFilter(5);
                newImage = new Bitmap(image);
                newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                newImage = filter.ApplyFilter(newImage);
                bool comicMode = this.DynamicParameter;

                if (comicMode)
                {
                    // Draw the edges.
                    edgeBitmap = new Bitmap(width, height);
                    edgeBitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                    edgeBitmap = Effects.Trace(image, edgeBitmap, 120);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        // Overlay the image.
                        graphics.DrawImage(edgeBitmap, 0, 0);
                        Rectangle rectangle = new Rectangle(0, 0, width, height);

                        // Draw an edge around the image.
                        using (Pen blackPen = new Pen(Color.Black))
                        {
                            blackPen.Width = 4;
                            graphics.DrawRectangle(blackPen, rectangle);
                        }
                    }

                    edgeBitmap.Dispose();
                }

                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (edgeBitmap != null)
                {
                    edgeBitmap.Dispose();
                }

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
