// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConvolutionFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The convolution filter for applying gradient operators to detect edges within an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.EdgeDetection
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Filters;

    /// <summary>
    /// The convolution filter for applying gradient operators to detect edges within an image.
    /// </summary>
    public class ConvolutionFilter
    {
        /// <summary>
        /// The edge filter.
        /// </summary>
        private readonly IEdgeFilter edgeFilter;

        /// <summary>
        /// Whether to produce a greyscale output.
        /// </summary>
        private readonly bool greyscale;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvolutionFilter"/> class.
        /// </summary>
        /// <param name="edgeFilter">
        /// The <see cref="IEdgeFilter"/> to apply.
        /// </param>
        /// <param name="greyscale">
        /// Whether to produce a greyscale output.
        /// </param>
        public ConvolutionFilter(IEdgeFilter edgeFilter, bool greyscale)
        {
            this.edgeFilter = edgeFilter;
            this.greyscale = greyscale;
        }

        /// <summary>
        /// Processes the given bitmap to apply the current instances <see cref="IEdgeFilter"/>.
        /// </summary>
        /// <param name="source">The image to process.</param>
        /// <returns>A processed bitmap.</returns>
        public Bitmap ProcessFilter(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;

            Bitmap destination = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Bitmap input = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(input))
            {
                Rectangle rectangle = new Rectangle(0, 0, width, height);
                if (this.greyscale)
                {
                    // If it's greyscale apply a colormatix to the image.
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(ColorMatrixes.GreyScale);
                        graphics.DrawImage(source, rectangle, 0, 0, width, height, GraphicsUnit.Pixel, attributes);
                    }
                }
                else
                {
                    // Fixes an issue with transparency not converting properly.
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(source, rectangle);
                }
            }

            try
            {
                double[,] horizontalFilter = this.edgeFilter.HorizontalGradientOperator;
                double[,] verticallFilter = this.edgeFilter.VerticalGradientOperator;

                int filterXOffset = (horizontalFilter.GetLength(1) - 1) / 2;
                int filterYOffset = (horizontalFilter.GetLength(0) - 1) / 2;
                int maxWidth = width - filterXOffset;
                int maxHeight = height - filterYOffset;

                using (FastBitmap sourceBitmap = new FastBitmap(input))
                {
                    using (FastBitmap destinationBitmap = new FastBitmap(destination))
                    {
                        // Loop through the pixels.
                        for (int y = filterYOffset; y < maxHeight; y++)
                        {
                            for (int x = filterXOffset; x < maxWidth; x++)
                            {
                                double rX = 0;
                                double rY = 0;
                                double gX = 0;
                                double gY = 0;
                                double bX = 0;
                                double bY = 0;

                                // Apply each matrix multiplier to the color components for each pixel.
                                for (int fy = -1; fy < filterYOffset; fy++)
                                {
                                    for (int fx = -1; fx < filterXOffset; fx++)
                                    {
                                        Color currentColor = sourceBitmap.GetPixel(x + fx, y + fy);
                                        double r = currentColor.R;
                                        double g = currentColor.G;
                                        double b = currentColor.B;

                                        rX += horizontalFilter[fy + 1, fx + 1] * r;
                                        rY += verticallFilter[fy + 1, fx + 1] * r;

                                        gX += horizontalFilter[fy + 1, fx + 1] * g;
                                        gY += verticallFilter[fy + 1, fx + 1] * g;

                                        bX += horizontalFilter[fy + 1, fx + 1] * b;
                                        bY += verticallFilter[fy + 1, fx + 1] * b;
                                    }
                                }

                                // Apply the equation and sanitize.
                                byte red = Math.Sqrt((rX * rX) + (rY * rY)).ToByte();
                                byte green = Math.Sqrt((gX * gX) + (gY * gY)).ToByte();
                                byte blue = Math.Sqrt((bX * bX) + (bY * bY)).ToByte();

                                Color newColor = Color.FromArgb(red, green, blue);
                                destinationBitmap.SetPixel(x, y, newColor);
                            }
                        }
                    }
                }
            }
            finally
            {
                // We created a new image. Cleanup.
                input.Dispose();
            }

            return destination;
        }
    }
}
