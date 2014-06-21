// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Resize.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Resizes an image to the given dimensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.Linq;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Resize"/> class.
        /// </summary>
        public Resize()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
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
        /// Gets or sets the list of sizes to restrict resizing methods to.
        /// </summary>
        public List<Size> RestrictedSizes { get; set; }

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
            int width = this.DynamicParameter.Size.Width ?? 0;
            int height = this.DynamicParameter.Size.Height ?? 0;
            ResizeMode mode = this.DynamicParameter.ResizeMode;
            AnchorPosition anchor = this.DynamicParameter.AnchorPosition;
            bool upscale = this.DynamicParameter.Upscale;
            float[] centerCoordinates = this.DynamicParameter.CenterCoordinates;

            int defaultMaxWidth;
            int defaultMaxHeight;

            int.TryParse(this.Settings["MaxWidth"], NumberStyles.Any, CultureInfo.InvariantCulture, out defaultMaxWidth);
            int.TryParse(this.Settings["MaxHeight"], NumberStyles.Any, CultureInfo.InvariantCulture, out defaultMaxHeight);

            return this.ResizeImage(factory, width, height, defaultMaxWidth, defaultMaxHeight, this.RestrictedSizes, mode, anchor, upscale, centerCoordinates);
        }

        /// <summary>
        /// The resize image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
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
        /// <param name="restrictedSizes">
        /// A <see cref="List{Size}"/> containing image resizing restrictions.
        /// </param>
        /// <param name="resizeMode">
        /// The mode with which to resize the image.
        /// </param>
        /// <param name="anchorPosition">
        /// The anchor position to place the image at.
        /// </param>
        /// <param name="upscale">
        /// Whether to allow up-scaling of images. (Default true)
        /// </param>
        /// <param name="centerCoordinates">
        /// If the resize mode is crop, you can set a specific center coordinate, use as alternative to anchorPosition
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        private Image ResizeImage(
            ImageFactory factory,
            int width,
            int height,
            int defaultMaxWidth,
            int defaultMaxHeight,
            List<Size> restrictedSizes,
            ResizeMode resizeMode = ResizeMode.Pad,
            AnchorPosition anchorPosition = AnchorPosition.Center,
            bool upscale = true,
            float[] centerCoordinates = null)
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
                        destinationWidth = (int)Math.Ceiling(sourceWidth * percentHeight);
                    }
                    else
                    {
                        ratio = percentWidth;
                        destinationY = (int)((height - (sourceHeight * ratio)) / 2);
                        destinationHeight = (int)Math.Ceiling(sourceHeight * percentWidth);
                    }
                }

                // Change the destination rectangle coordinates if cropping and 
                // there has been a set width and height.
                if (resizeMode == ResizeMode.Crop && width > 0 && height > 0)
                {
                    double ratio;

                    if (percentHeight < percentWidth)
                    {
                        ratio = percentWidth;

                        if (centerCoordinates != null && centerCoordinates.Any())
                        {
                            double center = -(ratio * sourceHeight) * centerCoordinates[0];
                            destinationY = (int)center + (height / 2);

                            if (destinationY > 0)
                            {
                                destinationY = 0;
                            }

                            if (destinationY < (int)(height - (sourceHeight * ratio)))
                            {
                                destinationY = (int)(height - (sourceHeight * ratio));
                            }
                        }
                        else
                        {
                            switch (anchorPosition)
                            {
                                case AnchorPosition.Top:
                                    destinationY = 0;
                                    break;
                                case AnchorPosition.Bottom:
                                    destinationY = (int)(height - (sourceHeight * ratio));
                                    break;
                                default:
                                    destinationY = (int)((height - (sourceHeight * ratio)) / 2);
                                    break;
                            }
                        }

                        destinationHeight = (int)Math.Ceiling(sourceHeight * percentWidth);
                    }
                    else
                    {
                        ratio = percentHeight;

                        if (centerCoordinates != null && centerCoordinates.Any())
                        {
                            double center = -(ratio * sourceWidth) * centerCoordinates[1];
                            destinationX = (int)center + (width / 2);

                            if (destinationX > 0)
                            {
                                destinationX = 0;
                            }

                            if (destinationX < (int)(width - (sourceWidth * ratio)))
                            {
                                destinationX = (int)(width - (sourceWidth * ratio));
                            }
                        }
                        else
                        {
                            switch (anchorPosition)
                            {
                                case AnchorPosition.Left:
                                    destinationX = 0;
                                    break;
                                case AnchorPosition.Right:
                                    destinationX = (int)(width - (sourceWidth * ratio));
                                    break;
                                default:
                                    destinationX = (int)((width - (sourceWidth * ratio)) / 2);
                                    break;
                            }
                        }

                        destinationWidth = (int)Math.Ceiling(sourceWidth * percentHeight);
                    }
                }

                // Constrain the image to fit the maximum possible height or width.
                if (resizeMode == ResizeMode.Max)
                {
                    // If either is 0, we don't need to figure out orientation
                    if (width > 0 && height > 0)
                    {
                        // Integers must be cast to doubles to get needed precision
                        double ratio = (double)height / width;
                        double sourceRatio = (double)sourceHeight / sourceWidth;

                        if (sourceRatio < ratio)
                        {
                            height = 0;
                        }
                        else
                        {
                            width = 0;
                        }
                    }
                }

                // If height or width is not passed we assume that the standard ratio is to be kept.
                if (height == 0)
                {
                    destinationHeight = (int)Math.Ceiling(sourceHeight * percentWidth);
                    height = destinationHeight;
                }

                if (width == 0)
                {
                    destinationWidth = (int)Math.Ceiling(sourceWidth * percentHeight);
                    width = destinationWidth;
                }

                // Restrict sizes
                if (restrictedSizes != null && restrictedSizes.Any())
                {
                    bool reject = true;
                    foreach (Size restrictedSize in restrictedSizes)
                    {
                        if (restrictedSize.Height == 0 || restrictedSize.Width == 0)
                        {
                            if (restrictedSize.Width == width || restrictedSize.Height == height)
                            {
                                reject = false;
                            }
                        }
                        else if (restrictedSize.Width == width && restrictedSize.Height == height)
                        {
                            reject = false;
                        }
                    }

                    if (reject)
                    {
                        return image;
                    }
                }

                if (width > 0 && height > 0 && width <= maxWidth && height <= maxHeight)
                {
                    // Exit if upscaling is not allowed.
                    if ((width > sourceWidth || height > sourceHeight) && upscale == false && resizeMode != ResizeMode.Stretch)
                    {
                        return image;
                    }

                    newImage = new Bitmap(width, height, PixelFormat.Format32bppPArgb);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        // We want to use two different blending algorithms for enlargement/shrinking.
                        if (image.Width < destinationWidth && image.Height < destinationHeight)
                        {
                            // We are making it larger.
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        }
                        else
                        {
                            // We are making it smaller.
                            graphics.SmoothingMode = SmoothingMode.None;
                        }

                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;

                        // An unwanted border appears when using InterpolationMode.HighQualityBicubic to resize the image
                        // as the algorithm appears to be pulling averaging detail from surrounding pixels beyond the edge
                        // of the image. Using the ImageAttributes class to specify that the pixels beyond are simply mirror
                        // images of the pixels within solves this problem.
                        using (ImageAttributes wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
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
    }
}