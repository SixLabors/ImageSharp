// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ComicMatrixFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a comic filter to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Filters.Artistic;
    using ImageProcessor.Imaging.Filters.EdgeDetection;

    /// <summary>
    /// Encapsulates methods with which to add a comic filter to an image.
    /// </summary>
    internal class ComicMatrixFilter : MatrixFilterBase
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for this filter instance.
        /// </summary>
        public override ColorMatrix Matrix
        {
            get { return ColorMatrixes.ComicLow; }
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
            // Bitmaps for comic pattern
            Bitmap highBitmap = null;
            Bitmap lowBitmap = null;
            Bitmap patternBitmap = null;
            Bitmap edgeBitmap = null;

            try
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                    attributes.SetColorMatrix(ColorMatrixes.ComicHigh);

                    // Draw the image with the high comic colormatrix.
                    highBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Apply a oil painting filter to the image.
                    highBitmap = new OilPaintingFilter(3, 5).ApplyFilter((Bitmap)image);

                    // Draw the edges.
                    edgeBitmap = Trace((Bitmap)image, 120);

                    using (Graphics graphics = Graphics.FromImage(highBitmap))
                    {
                        graphics.DrawImage(highBitmap, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }

                    // Create a bitmap for overlaying.
                    lowBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Set the color matrix
                    attributes.SetColorMatrix(this.Matrix);

                    // Draw the image with the losatch colormatrix.
                    using (Graphics graphics = Graphics.FromImage(lowBitmap))
                    {
                        graphics.DrawImage(highBitmap, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }

                    // We need to create a new image now with a pattern mask to paint it
                    // onto the other image with.
                    patternBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Create the pattern mask.
                    using (Graphics graphics = Graphics.FromImage(patternBitmap))
                    {
                        graphics.Clear(Color.Black);
                        graphics.SmoothingMode = SmoothingMode.HighQuality;

                        for (int y = 0; y < image.Height; y += 8)
                        {
                            for (int x = 0; x < image.Width; x += 4)
                            {
                                graphics.FillEllipse(Brushes.White, x, y, 3, 3);
                                graphics.FillEllipse(Brushes.White, x + 2, y + 4, 3, 3);
                            }
                        }
                    }

                    // Transfer the alpha channel from the mask to the high saturation image.
                    ApplyMask(patternBitmap, lowBitmap);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        graphics.Clear(Color.Transparent);

                        // Overlay the image.
                        graphics.DrawImage(highBitmap, 0, 0);
                        graphics.DrawImage(lowBitmap, 0, 0);
                        graphics.DrawImage(edgeBitmap, 0, 0);

                        // Draw an edge around the image.
                        using (Pen blackPen = new Pen(Color.Black))
                        {
                            blackPen.Width = 4;
                            graphics.DrawRectangle(blackPen, rectangle);
                        }

                        // Dispose of the other images
                        highBitmap.Dispose();
                        lowBitmap.Dispose();
                        patternBitmap.Dispose();
                        edgeBitmap.Dispose();
                    }
                }

                // Reassign the image.
                image.Dispose();
                image = newImage;
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                if (highBitmap != null)
                {
                    highBitmap.Dispose();
                }

                if (lowBitmap != null)
                {
                    lowBitmap.Dispose();
                }

                if (patternBitmap != null)
                {
                    patternBitmap.Dispose();
                }

                if (edgeBitmap != null)
                {
                    edgeBitmap.Dispose();
                }
            }

            return image;
        }

        /// <summary>
        /// Detects and draws edges.
        /// TODO: Move this to another class and do edge detection.
        /// </summary>
        /// <param name="source">
        /// The source bitmap.
        /// </param>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/>.
        /// </returns>
        private static Bitmap Trace(Bitmap source, byte threshold = 0)
        {
            int width = source.Width;
            int height = source.Height;

            // Grab the edges converting to greyscale, and invert the colors.
            ConvolutionFilter filter = new ConvolutionFilter(new SobelEdgeFilter(), true);
            Bitmap destination = filter.Process2DFilter(source);
            Bitmap invert = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            InvertMatrixFilter matrix = new InvertMatrixFilter();
            invert = (Bitmap)matrix.TransformImage(destination, invert);

            // Loop through and replace any colors more white than the threshold
            // with a transparent one. 
            using (FastBitmap sourceBitmap = new FastBitmap(invert))
            {
                Parallel.For(
                    0,
                    height,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // ReSharper disable AccessToDisposedClosure
                            Color color = sourceBitmap.GetPixel(x, y);
                            if (color.B >= threshold)
                            {
                                sourceBitmap.SetPixel(x, y, Color.Transparent);
                            }
                            // ReSharper restore AccessToDisposedClosure
                        }
                    });
            }

            destination.Dispose();
            destination = invert;

            return destination;
        }

        /// <summary>
        /// Applies a mask .
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="destination">
        /// The destination.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if the two images are of different size.
        /// </exception>
        private static void ApplyMask(Bitmap source, Bitmap destination)
        {
            if (source.Size != destination.Size)
            {
                throw new ArgumentException();
            }

            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                using (FastBitmap destinationBitmap = new FastBitmap(destination))
                {
                    int width = source.Width;
                    int height = source.Height;

                    Parallel.For(
                        0,
                        height,
                        y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                // ReSharper disable AccessToDisposedClosure
                                Color sourceColor = sourceBitmap.GetPixel(x, y);
                                Color destinationColor = destinationBitmap.GetPixel(x, y);

                                if (destinationColor.A != 0)
                                {
                                    destinationBitmap.SetPixel(x, y, Color.FromArgb(sourceColor.B, destinationColor.R, destinationColor.G, destinationColor.B));
                                }

                                // ReSharper restore AccessToDisposedClosure
                            }
                        });
                }
            }
        }
    }
}