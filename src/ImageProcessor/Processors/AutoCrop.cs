// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AutoCrop.cs" company="James South">
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
    using ImageProcessor.Imaging.Binarization;
    using ImageProcessor.Imaging.EdgeDetection;

    /// <summary>
    /// The auto crop.
    /// </summary>
    public class AutoCrop : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoCrop"/> class.
        /// </summary>
        public AutoCrop()
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
                grey = new BinaryThreshold(threshold).ProcessFilter(grey);//.Clone(new Rectangle(0, 0, grey.Width, grey.Height), PixelFormat.Format8bppIndexed);

                // lock source bitmap data
                //BitmapData data = grey.LockBits(new Rectangle(0, 0, grey.Width, grey.Height), ImageLockMode.ReadOnly, grey.PixelFormat);
                //Rectangle rectangle = this.FindBoxExactgreyscale(data, 0);
                //grey.UnlockBits(data);

                Rectangle rectangle = FindBox(grey, 0);

                newImage = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    // An unwanted border appears when using InterpolationMode.HighQualityBicubic to resize the image
                    // as the algorithm appears to be pulling averaging detail from surrounding pixels beyond the edge 
                    // of the image. Using the ImageAttributes class to specify that the pixels beyond are simply mirror 
                    // images of the pixels within solves this problem.
                    using (ImageAttributes wrapMode = new ImageAttributes())
                    {
                        graphics.DrawImage(
                                         image,
                                         new Rectangle(0, 0, rectangle.Width, rectangle.Height),
                                         rectangle.X,
                                         rectangle.Y,
                                         rectangle.Width,
                                         rectangle.Height,
                                         GraphicsUnit.Pixel,
                                         wrapMode);
                    }
                }

                // Reassign the image.
                //grey.Dispose();
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
        /// Returns a bounding box that only excludes the specified color. 
        /// Only works on 8-bit images.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="colorToRemove">The palette index to remove.</param>
        /// <returns></returns>
        private Rectangle FindBoxExactgreyscale(BitmapData sourceData, byte indexToRemove)
        {
            if (sourceData.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentOutOfRangeException("FindBoxExact only operates on 8-bit greyscale images");
            // get source image size
            int width = sourceData.Width;
            int height = sourceData.Height;
            int offset = sourceData.Stride - width;

            int minX = width;
            int minY = height;
            int maxX = 0;
            int maxY = 0;

            // find rectangle which contains something except color to remove
            unsafe
            {
                byte* src = (byte*)sourceData.Scan0;

                for (int y = 0; y < height; y++)
                {
                    if (y > 0) src += offset; //Don't adjust for offset until after first row
                    for (int x = 0; x < width; x++)
                    {
                        if (x > 0 || y > 0) src++; //Don't increment until after the first pixel.
                        if (*src != indexToRemove)
                        {
                            if (x < minX)
                                minX = x;
                            if (x > maxX)
                                maxX = x;
                            if (y < minY)
                                minY = y;
                            if (y > maxY)
                                maxY = y;
                        }
                    }
                }
            }

            // check
            if ((minX == width) && (minY == height) && (maxX == 0) && (maxY == 0))
            {
                minX = minY = 0;
            }

            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }


        private Rectangle FindBox(Bitmap bitmap, byte indexToRemove)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int startX = width;
            int startY = height;
            int stopX = 0;
            int stopY = 0;

            using (FastBitmap fastBitmap = new FastBitmap(bitmap))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (fastBitmap.GetPixel(x, y).B != indexToRemove)
                        {
                            if (x < startX)
                            {
                                startX = x;
                            }

                            if (x > stopX)
                            {
                                stopX = x;
                            }

                            if (y < startY)
                            {
                                startY = y;
                            }

                            if (y > stopY)
                            {
                                stopY = y;
                            }
                        }
                    }
                }
            }

            // check
            if ((startX == width) && (startY == height) && (stopX == 0) && (stopY == 0))
            {
                startX = startY = 0;
            }

            return new Rectangle(startX, startY, stopX - startX + 1, stopY - startY + 1);
        }
    }
}