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
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Threading.Tasks;

    using ImageProcessor.Imaging.Filters.Artistic;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Imaging.Helpers;

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
        /// The processed <see cref="System.Drawing.Bitmap"/>.
        /// </returns>
        public override Bitmap TransformImage(Image image, Image newImage)
        {
            // Bitmaps for comic pattern
            Bitmap highBitmap = null;
            Bitmap lowBitmap = null;
            Bitmap patternBitmap = null;
            Bitmap edgeBitmap = null;
            int width = image.Width;
            int height = image.Height;

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
                    edgeBitmap = new Bitmap(width, height);
                    edgeBitmap = Trace(image, edgeBitmap, 120);

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
                        graphics.Clear(Color.Transparent);
                        graphics.SmoothingMode = SmoothingMode.HighQuality;

                        for (int y = 0; y < height; y += 8)
                        {
                            for (int x = 0; x < width; x += 4)
                            {
                                graphics.FillEllipse(Brushes.White, x, y, 3, 3);
                                graphics.FillEllipse(Brushes.White, x + 2, y + 4, 3, 3);
                            }
                        }
                    }

                    // Transfer the alpha channel from the mask to the low saturation image.
                    lowBitmap = Effects.ApplyMask(lowBitmap, patternBitmap);

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

            return (Bitmap)image;
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