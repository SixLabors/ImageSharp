// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConvolutionFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The convolution filter for applying gradient operators to detect edges within an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.EdgeDetection
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Filters.Photo;

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
        /// Processes the given bitmap to apply the current instance of <see cref="IEdgeFilter"/>.
        /// </summary>
        /// <param name="source">The image to process.</param>
        /// <returns>A processed bitmap.</returns>
        public Bitmap ProcessFilter(Image source)
        {
            int width = source.Width;
            int height = source.Height;
            int maxWidth = width + 1;
            int maxHeight = height + 1;
            int bufferedWidth = width + 2;
            int bufferedHeight = height + 2;

            Bitmap destination = new Bitmap(width, height);
            Bitmap input = new Bitmap(bufferedWidth, bufferedHeight);
            destination.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            input.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(input))
            {
                // Fixes an issue with transparency not converting properly.
                graphics.Clear(Color.Transparent);

                Rectangle destinationRectangle = new Rectangle(0, 0, bufferedWidth, bufferedHeight);
                Rectangle rectangle = new Rectangle(0, 0, width, height);

                // If it's greyscale apply a colormatrix to the image.
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    if (this.greyscale)
                    {
                        attributes.SetColorMatrix(ColorMatrixes.GreyScale);
                    }

                    // We use a trick here to detect right to the edges of the image.
                    // flip/tile the image with a pixel in excess in each direction to duplicate pixels.
                    // Later on we draw pixels without that excess.
                    using (TextureBrush tb = new TextureBrush(source, rectangle, attributes))
                    {
                        tb.WrapMode = WrapMode.TileFlipXY;
                        tb.TranslateTransform(1, 1);

                        graphics.FillRectangle(tb, destinationRectangle);
                    }
                }
            }

            try
            {
                double[,] horizontalFilter = this.edgeFilter.HorizontalGradientOperator;

                int kernelLength = horizontalFilter.GetLength(0);
                int radius = kernelLength >> 1;

                using (FastBitmap sourceBitmap = new FastBitmap(input))
                {
                    using (FastBitmap destinationBitmap = new FastBitmap(destination))
                    {
                        // Loop through the pixels.
                        Parallel.For(
                            0,
                            bufferedHeight,
                            y =>
                            {
                                for (int x = 0; x < bufferedWidth; x++)
                                {
                                    double rX = 0;
                                    double gX = 0;
                                    double bX = 0;

                                    // Apply each matrix multiplier to the color components for each pixel.
                                    for (int fy = 0; fy < kernelLength; fy++)
                                    {
                                        int fyr = fy - radius;
                                        int offsetY = y + fyr;

                                        // Skip the current row
                                        if (offsetY < 0)
                                        {
                                            continue;
                                        }

                                        // Outwith the current bounds so break.
                                        if (offsetY >= bufferedHeight)
                                        {
                                            break;
                                        }

                                        for (int fx = 0; fx < kernelLength; fx++)
                                        {
                                            int fxr = fx - radius;
                                            int offsetX = x + fxr;

                                            // Skip the column
                                            if (offsetX < 0)
                                            {
                                                continue;
                                            }

                                            if (offsetX < bufferedWidth)
                                            {
                                                // ReSharper disable once AccessToDisposedClosure
                                                Color currentColor = sourceBitmap.GetPixel(offsetX, offsetY);
                                                double r = currentColor.R;
                                                double g = currentColor.G;
                                                double b = currentColor.B;

                                                rX += horizontalFilter[fy, fx] * r;

                                                gX += horizontalFilter[fy, fx] * g;

                                                bX += horizontalFilter[fy, fx] * b;
                                            }
                                        }
                                    }

                                    // Apply the equation and sanitize.
                                    byte red = rX.ToByte();
                                    byte green = gX.ToByte();
                                    byte blue = bX.ToByte();

                                    Color newColor = Color.FromArgb(red, green, blue);
                                    if (y > 0 && x > 0 && y < maxHeight && x < maxWidth)
                                    {
                                        // ReSharper disable once AccessToDisposedClosure
                                        destinationBitmap.SetPixel(x - 1, y - 1, newColor);
                                    }
                                }
                            });
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

        /// <summary>
        /// Processes the given bitmap to apply the current instance of <see cref="I2DEdgeFilter"/>.
        /// </summary>
        /// <param name="source">The image to process.</param>
        /// <returns>A processed bitmap.</returns>
        public Bitmap Process2DFilter(Image source)
        {
            int width = source.Width;
            int height = source.Height;
            int maxWidth = width + 1;
            int maxHeight = height + 1;
            int bufferedWidth = width + 2;
            int bufferedHeight = height + 2;

            Bitmap destination = new Bitmap(width, height);
            Bitmap input = new Bitmap(bufferedWidth, bufferedHeight);
            destination.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            input.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(input))
            {
                // Fixes an issue with transparency not converting properly.
                graphics.Clear(Color.Transparent);

                Rectangle destinationRectangle = new Rectangle(0, 0, bufferedWidth, bufferedHeight);
                Rectangle rectangle = new Rectangle(0, 0, width, height);

                // If it's greyscale apply a colormatrix to the image.
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    if (this.greyscale)
                    {
                        attributes.SetColorMatrix(ColorMatrixes.GreyScale);
                    }

                    // We use a trick here to detect right to the edges of the image.
                    // flip/tile the image with a pixel in excess in each direction to duplicate pixels.
                    // Later on we draw pixels without that excess.
                    using (TextureBrush tb = new TextureBrush(source, rectangle, attributes))
                    {
                        tb.WrapMode = WrapMode.TileFlipXY;
                        tb.TranslateTransform(1, 1);

                        graphics.FillRectangle(tb, destinationRectangle);
                    }
                }
            }

            try
            {
                double[,] horizontalFilter = this.edgeFilter.HorizontalGradientOperator;
                double[,] verticalFilter = ((I2DEdgeFilter)this.edgeFilter).VerticalGradientOperator;

                int kernelLength = horizontalFilter.GetLength(0);
                int radius = kernelLength >> 1;

                using (FastBitmap sourceBitmap = new FastBitmap(input))
                {
                    using (FastBitmap destinationBitmap = new FastBitmap(destination))
                    {
                        // Loop through the pixels.
                        Parallel.For(
                            0,
                            bufferedHeight,
                            y =>
                            {
                                for (int x = 0; x < bufferedWidth; x++)
                                {
                                    double rX = 0;
                                    double rY = 0;
                                    double gX = 0;
                                    double gY = 0;
                                    double bX = 0;
                                    double bY = 0;

                                    // Apply each matrix multiplier to the color components for each pixel.
                                    for (int fy = 0; fy < kernelLength; fy++)
                                    {
                                        int fyr = fy - radius;
                                        int offsetY = y + fyr;

                                        // Skip the current row
                                        if (offsetY < 0)
                                        {
                                            continue;
                                        }

                                        // Outwith the current bounds so break.
                                        if (offsetY >= bufferedHeight)
                                        {
                                            break;
                                        }

                                        for (int fx = 0; fx < kernelLength; fx++)
                                        {
                                            int fxr = fx - radius;
                                            int offsetX = x + fxr;

                                            // Skip the column
                                            if (offsetX < 0)
                                            {
                                                continue;
                                            }

                                            if (offsetX < bufferedWidth)
                                            {
                                                // ReSharper disable once AccessToDisposedClosure
                                                Color currentColor = sourceBitmap.GetPixel(offsetX, offsetY);
                                                double r = currentColor.R;
                                                double g = currentColor.G;
                                                double b = currentColor.B;

                                                rX += horizontalFilter[fy, fx] * r;
                                                rY += verticalFilter[fy, fx] * r;

                                                gX += horizontalFilter[fy, fx] * g;
                                                gY += verticalFilter[fy, fx] * g;

                                                bX += horizontalFilter[fy, fx] * b;
                                                bY += verticalFilter[fy, fx] * b;
                                            }
                                        }
                                    }

                                    // Apply the equation and sanitize.
                                    byte red = Math.Sqrt((rX * rX) + (rY * rY)).ToByte();
                                    byte green = Math.Sqrt((gX * gX) + (gY * gY)).ToByte();
                                    byte blue = Math.Sqrt((bX * bX) + (bY * bY)).ToByte();

                                    Color newColor = Color.FromArgb(red, green, blue);
                                    if (y > 0 && x > 0 && y < maxHeight && x < maxWidth)
                                    {
                                        // ReSharper disable once AccessToDisposedClosure
                                        destinationBitmap.SetPixel(x - 1, y - 1, newColor);
                                    }
                                }
                            });
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
