// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Resizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods to resize images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Linq;

    using ImageProcessor.Common.Exceptions;

    /// <summary>
    /// Provides methods to resize images.
    /// </summary>
    public class Resizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Resizer"/> class.
        /// </summary>
        /// <param name="size">
        /// The <see cref="Size"/> to resize the image to.
        /// </param>
        public Resizer(Size size)
        {
            this.ResizeLayer = new ResizeLayer(size);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Resizer"/> class.
        /// </summary>
        /// <param name="resizeLayer">
        /// The <see cref="ResizeLayer"/>.
        /// </param>
        public Resizer(ResizeLayer resizeLayer)
        {
            this.ResizeLayer = resizeLayer;
        }

        /// <summary>
        /// Gets or sets the <see cref="ResizeLayer"/>.
        /// </summary>
        public ResizeLayer ResizeLayer { get; set; }

        /// <summary>
        /// Resizes the given image.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="Image"/> to resize
        /// </param>
        /// <returns>
        /// The resized <see cref="Image"/>.
        /// </returns>
        public Bitmap ResizeImage(Image source)
        {
            int width = this.ResizeLayer.Size.Width;
            int height = this.ResizeLayer.Size.Height;
            ResizeMode mode = this.ResizeLayer.ResizeMode;
            AnchorPosition anchor = this.ResizeLayer.AnchorPosition;
            bool upscale = this.ResizeLayer.Upscale;
            float[] centerCoordinates = this.ResizeLayer.CenterCoordinates;
            int maxWidth = this.ResizeLayer.MaxSize.HasValue ? this.ResizeLayer.MaxSize.Value.Width : int.MaxValue;
            int maxHeight = this.ResizeLayer.MaxSize.HasValue ? this.ResizeLayer.MaxSize.Value.Height : int.MaxValue;
            List<Size> restrictedSizes = this.ResizeLayer.RestrictedSizes;

            return this.ResizeImage(source, width, height, maxWidth, maxHeight, restrictedSizes, mode, anchor, upscale, centerCoordinates);
        }

        /// <summary>
        /// Resizes the given image.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="Image"/> to resize
        /// </param>
        /// <param name="width">
        /// The width to resize the image to.
        /// </param>
        /// <param name="height">
        /// The height to resize the image to.
        /// </param>
        /// <param name="maxWidth">
        /// The default max width to resize the image to.
        /// </param>
        /// <param name="maxHeight">
        /// The default max height to resize the image to.
        /// </param>
        /// <param name="restrictedSizes">
        /// A <see cref="List{T}"/> containing image resizing restrictions.
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
        /// The resized <see cref="Image"/>.
        /// </returns>
        private Bitmap ResizeImage(
            Image source,
            int width,
            int height,
            int maxWidth,
            int maxHeight,
            List<Size> restrictedSizes,
            ResizeMode resizeMode = ResizeMode.Pad,
            AnchorPosition anchorPosition = AnchorPosition.Center,
            bool upscale = true,
            float[] centerCoordinates = null)
        {
            Bitmap newImage = null;

            try
            {
                int sourceWidth = source.Width;
                int sourceHeight = source.Height;

                int destinationWidth = width;
                int destinationHeight = height;

                maxWidth = maxWidth > 0 ? maxWidth : int.MaxValue;
                maxHeight = maxHeight > 0 ? maxHeight : int.MaxValue;

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
                        destinationWidth = Convert.ToInt32(sourceWidth * percentHeight);

                        switch (anchorPosition)
                        {
                            case AnchorPosition.Left:
                                destinationX = 0;
                                break;
                            case AnchorPosition.Right:
                                destinationX = (int)(width - (sourceWidth * ratio));
                                break;
                            default:
                                destinationX = Convert.ToInt32((width - (sourceWidth * ratio)) / 2);
                                break;
                        }
                    }
                    else
                    {
                        ratio = percentWidth;
                        destinationHeight = Convert.ToInt32(sourceHeight * percentWidth);

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
                    destinationHeight = Convert.ToInt32(sourceHeight * percentWidth);
                    height = destinationHeight;
                }

                if (width == 0)
                {
                    destinationWidth = Convert.ToInt32(sourceWidth * percentHeight);
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
                        return (Bitmap)source;
                    }
                }

                if (width > 0 && height > 0 && width <= maxWidth && height <= maxHeight)
                {
                    // Exit if upscaling is not allowed.
                    if ((width > sourceWidth || height > sourceHeight) && upscale == false && resizeMode != ResizeMode.Stretch)
                    {
                        return (Bitmap)source;
                    }

                    newImage = new Bitmap(width, height);
                    newImage.SetResolution(source.HorizontalResolution, source.VerticalResolution);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        // We want to use two different blending algorithms for enlargement/shrinking.
                        if (source.Width < destinationWidth && source.Height < destinationHeight)
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
                            Rectangle destinationRectangle = new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight);
                            graphics.DrawImage(source, destinationRectangle, 0, 0, sourceWidth, sourceHeight, GraphicsUnit.Pixel, wrapMode);
                        }

                        // Reassign the image.
                        source.Dispose();
                        source = newImage;
                    }
                }
            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }

            return (Bitmap)source;
        }
    }
}
