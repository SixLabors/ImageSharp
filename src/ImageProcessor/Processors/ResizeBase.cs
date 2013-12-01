// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ResizeBase.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The resize base for inheriting resizable methods from.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;

    using ImageProcessor.Imaging;

    #endregion

    /// <summary>
    /// The resize base for inheriting resizable methods from.
    /// </summary>
    public abstract class ResizeBase : IGraphicsProcessor
    {
        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public abstract Regex RegexPattern { get; }

        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public abstract dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets or sets the order in which this processor is to be used in a chain.
        /// </summary>
        public abstract int SortOrder { get; protected set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public abstract Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public abstract int MatchRegexIndex(string queryString);

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public abstract Image ProcessImage(ImageFactory factory);

        /// <summary>
        /// The resize image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <param name="width">
        /// The width to resize the image to.
        /// </param>
        /// <param name="height">
        /// The height to resize the image to.
        /// </param>
        /// <param name="defaultMaxWidth">
        /// The default max width to resize the image to.
        /// </param>
        /// <param name="defaultMaxHeight">
        /// The default max height to resize the image to.
        /// </param>
        /// <param name="resizeMode">
        /// Whether to pad the image to fill the set size.
        /// </param>
        /// <param name="backgroundColor">
        /// The background color to pad the image with.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        protected Image ResizeImage(ImageFactory factory, int width, int height, int defaultMaxWidth, int defaultMaxHeight, ResizeMode resizeMode, Color backgroundColor)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;

                int destinationWidth = width;
                int destinationHeight = height;

                int maxWidth = defaultMaxWidth > 0 ? defaultMaxWidth : int.MaxValue;
                int maxHeight = defaultMaxHeight > 0 ? defaultMaxHeight : int.MaxValue;

                // Fractional variants for preserving aspect ratio.
                double percentHeight = Math.Abs(height / (double)sourceHeight);
                double percentWidth = Math.Abs(width / (double)sourceWidth);

                int destinationX = 0;
                int destinationY = 0;

                // Change the destination rectangle coordinates if padding and 
                // there has been a set width and height.
                if (resizeMode == ResizeMode.Pad && width > 0 && height > 0)
                {
                    double ratio;

                    if (percentHeight < percentWidth)
                    {
                        ratio = percentHeight;
                        destinationX = (int)((width - (sourceWidth * ratio)) / 2);
                        destinationWidth = (int)Math.Floor(sourceWidth * percentHeight);
                    }
                    else
                    {
                        ratio = percentWidth;
                        destinationY = (int)((height - (sourceHeight * ratio)) / 2);
                        destinationHeight = (int)Math.Floor(sourceHeight * percentWidth);
                    }
                }

                // If height or width is not passed we assume that the standard ratio is to be kept.
                if (height == 0)
                {
                    destinationHeight = (int)Math.Floor(sourceHeight * percentWidth);
                    height = destinationHeight;
                }

                if (width == 0)
                {
                    destinationWidth = (int)Math.Floor(sourceWidth * percentHeight);
                    width = destinationWidth;
                }

                if (width > 0 && height > 0 && width <= maxWidth && height <= maxHeight)
                {
                    // Don't use an object initializer here.
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    newImage = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                    newImage.Tag = image.Tag;

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        // We want to use two different blending algorithms for enlargement/shrinking.
                        // Bicubic is better enlarging for whilst Bilinear is better for shrinking.
                        // http://www.codinghorror.com/blog/2007/07/better-image-resizing.html
                        if (image.Width < destinationWidth && image.Height < destinationHeight)
                        {
                            // We are making it larger.
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                        }
                        else
                        {
                            // We are making it smaller.
                            graphics.SmoothingMode = SmoothingMode.None;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;
                        }

                        // An unwanted border appears when using InterpolationMode.HighQualityBicubic to resize the image
                        // as the algorithm appears to be pulling averaging detail from surFlooring pixels beyond the edge 
                        // of the image. Using the ImageAttributes class to specify that the pixels beyond are simply mirror 
                        // images of the pixels within solves this problem.
                        using (ImageAttributes wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            graphics.Clear(backgroundColor);
                            Rectangle destRect = new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight);
                            graphics.DrawImage(image, destRect, 0, 0, sourceWidth, sourceHeight, GraphicsUnit.Pixel, wrapMode);
                        }

                        // Reassign the image.
                        image.Dispose();
                        image = newImage;
                    }
                }
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return image;
        }
        #endregion
    }
}