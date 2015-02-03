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
    using System.Drawing.Imaging;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.Artistic;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Imaging.Filters.Photo;
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

                // Draw the edges.
                edgeBitmap = new Bitmap(width, height);
                edgeBitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                edgeBitmap = Trace(image, edgeBitmap, 120);

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

        /// <summary>
        /// Traces the edges of a given <see cref="Image"/>.
        /// TODO: Move this to another class.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="Image"/>.
        /// </param>
        /// <param name="destination">
        /// The destination <see cref="Image"/>.
        /// </param>
        /// <param name="threshold">
        /// The threshold (between 0 and 255).
        /// </param>
        /// <returns>
        /// The a new instance of <see cref="Bitmap"/> traced.
        /// </returns>
        private static Bitmap Trace(Image source, Image destination, byte threshold = 0)
        {
            int width = source.Width;
            int height = source.Height;

            // Grab the edges converting to greyscale, and invert the colors.
            ConvolutionFilter filter = new ConvolutionFilter(new SobelEdgeFilter(), true);

            using (Bitmap temp = filter.Process2DFilter(source))
            {
                destination = new InvertMatrixFilter().TransformImage(temp, destination);

                // Darken it slightly to aid detection
                destination = Adjustments.Brightness(destination, -5);
            }

            // Loop through and replace any colors more white than the threshold
            // with a transparent one. 
            using (FastBitmap destinationBitmap = new FastBitmap(destination))
            {
                Parallel.For(
                    0,
                    height,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // ReSharper disable AccessToDisposedClosure
                            Color color = destinationBitmap.GetPixel(x, y);
                            if (color.B >= threshold)
                            {
                                destinationBitmap.SetPixel(x, y, Color.Transparent);
                            }
                            // ReSharper restore AccessToDisposedClosure
                        }
                    });
            }

            // Darken it again to average out the color.
            destination = Adjustments.Brightness(destination, -5);
            return (Bitmap)destination;
        }
    }
}
