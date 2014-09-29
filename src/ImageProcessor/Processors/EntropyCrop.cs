// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EntropyCrop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.Binarization;
    using ImageProcessor.Imaging.Filters.EdgeDetection;

    /// <summary>
    /// The auto crop.
    /// </summary>
    public class EntropyCrop : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCrop"/> class.
        /// </summary>
        public EntropyCrop()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the dynamic parameter.
        /// </summary>
        public dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

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
            Bitmap newImage = null;
            Bitmap grey = null;
            Image image = factory.Image;
            byte threshold = this.DynamicParameter;

            try
            {
                grey = new ConvolutionFilter(new SobelEdgeFilter(), true).ProcessFilter((Bitmap)image);
                grey = new BinaryThreshold(threshold).ProcessFilter(grey);

                Rectangle rectangle = this.FindBoundingBox(grey, 0);

                newImage = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    graphics.DrawImage(
                                     image,
                                     new Rectangle(0, 0, rectangle.Width, rectangle.Height),
                                     rectangle.X,
                                     rectangle.Y,
                                     rectangle.Width,
                                     rectangle.Height,
                                     GraphicsUnit.Pixel);
                }

                // Reassign the image.
                grey.Dispose();
                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                if (grey != null)
                {
                    grey.Dispose();
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
        /// Finds the bounding rectangle based on the first instance of any color component other
        /// than the given one.
        /// </summary>
        /// <param name="bitmap">
        /// The bitmap.
        /// </param>
        /// <param name="componentToRemove">
        /// The color component to remove.
        /// </param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        private Rectangle FindBoundingBox(Bitmap bitmap, byte componentToRemove)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int startX;
            int startY;
            int stopX;
            int stopY;

            Func<FastBitmap, int> getMinY = fastBitmap =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (fastBitmap.GetPixel(x, y).B != componentToRemove)
                        {
                            return y;
                        }
                    }
                }

                return 0;
            };

            Func<FastBitmap, int> getMaxY = fastBitmap =>
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (fastBitmap.GetPixel(x, y).B != componentToRemove)
                        {
                            return y;
                        }
                    }
                }

                return height;
            };

            Func<FastBitmap, int> getMinX = fastBitmap =>
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (fastBitmap.GetPixel(x, y).B != componentToRemove)
                        {
                            return x;
                        }
                    }
                }

                return 0;
            };

            Func<FastBitmap, int> getMaxX = fastBitmap =>
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (fastBitmap.GetPixel(x, y).B != componentToRemove)
                        {
                            return x;
                        }
                    }
                }

                return height;
            };

            using (FastBitmap fastBitmap = new FastBitmap(bitmap))
            {
                startY = getMinY(fastBitmap);
                stopY = getMaxY(fastBitmap);
                startX = getMinX(fastBitmap);
                stopX = getMaxX(fastBitmap);
            }

            return new Rectangle(startX, startY, stopX - startX + 1, stopY - startY + 1);
        }
    }
}