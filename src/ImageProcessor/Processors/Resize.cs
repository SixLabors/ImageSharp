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
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;

    #endregion

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"((width|height)=\d+)|(mode=(pad|stretch|crop|max))|(anchor=(top|bottom|left|right|center))|(bgcolor=([0-9a-fA-F]{3}){1,2})|(upscale=false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the size attribute.
        /// </summary>
        private static readonly Regex SizeRegex = new Regex(@"(width|height)=\d+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the mode attribute.
        /// </summary>
        private static readonly Regex ModeRegex = new Regex(@"mode=(pad|stretch|crop|max)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the anchor attribute.
        /// </summary>
        private static readonly Regex AnchorRegex = new Regex(@"anchor=(top|bottom|left|right|center)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the color attribute.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"bgcolor=([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the upscale attribute.
        /// </summary>
        private static readonly Regex UpscaleRegex = new Regex(@"upscale=false", RegexOptions.Compiled);
        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public Regex RegexPattern
        {
            get
            {
                return QueryRegex;
            }
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
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder
        {
            get;
            private set;
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
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public int MatchRegexIndex(string queryString)
        {
            int index = 0;

            // Set the sort order to max to allow filtering.
            this.SortOrder = int.MaxValue;

            // First merge the matches so we can parse .
            StringBuilder stringBuilder = new StringBuilder();

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;
                    }

                    stringBuilder.Append(match.Value);

                    index += 1;
                }
            }

            // Match syntax
            string toParse = stringBuilder.ToString();

            Size size = this.ParseSize(toParse);
            ResizeLayer resizeLayer = new ResizeLayer(size)
                                          {
                                              ResizeMode = this.ParseMode(toParse),
                                              AnchorPosition = this.ParsePosition(toParse),
                                              BackgroundColor = this.ParseColor(toParse),
                                              Upscale = !UpscaleRegex.IsMatch(toParse)
                                          };

            this.DynamicParameter = resizeLayer;
            return this.SortOrder;
        }

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
        public Image ProcessImage(ImageFactory factory)
        {
            int width = this.DynamicParameter.Size.Width ?? 0;
            int height = this.DynamicParameter.Size.Height ?? 0;
            ResizeMode mode = this.DynamicParameter.ResizeMode;
            AnchorPosition anchor = this.DynamicParameter.AnchorPosition;
            Color backgroundColor = this.DynamicParameter.BackgroundColor;
            bool upscale = this.DynamicParameter.Upscale;

            int defaultMaxWidth;
            int defaultMaxHeight;
            int.TryParse(this.Settings["MaxWidth"], out defaultMaxWidth);
            int.TryParse(this.Settings["MaxHeight"], out defaultMaxHeight);

            return this.ResizeImage(factory, width, height, defaultMaxWidth, defaultMaxHeight, backgroundColor, mode, anchor, upscale);
        }
        #endregion

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
        /// <param name="backgroundColor">
        /// The background color to pad the image with.
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
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        private Image ResizeImage(
            ImageFactory factory,
            int width,
            int height,
            int defaultMaxWidth,
            int defaultMaxHeight,
            Color backgroundColor,
            ResizeMode resizeMode = ResizeMode.Pad,
            AnchorPosition anchorPosition = AnchorPosition.Center,
            bool upscale = true)
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

                // Change the destination rectangle coordinates if cropping and 
                // there has been a set width and height.
                if (resizeMode == ResizeMode.Crop && width > 0 && height > 0)
                {
                    double ratio;

                    if (percentHeight < percentWidth)
                    {
                        ratio = percentWidth;

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

                        destinationHeight = (int)Math.Floor(sourceHeight * percentWidth);
                    }
                    else
                    {
                        ratio = percentHeight;

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

                        destinationWidth = (int)Math.Floor(sourceWidth * percentHeight);
                    }
                }

                // Constrain the image to fit the maximum possible height or width.
                if (resizeMode == ResizeMode.Max)
                {
                    if (sourceWidth > width || sourceHeight > height)
                    {
                        double ratio = Math.Abs(height / width);
                        double sourceRatio = Math.Abs(sourceHeight / sourceWidth);

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
                    // Exit if upscaling is not allowed.
                    if ((width > sourceWidth || height > sourceHeight) && upscale == false && resizeMode != ResizeMode.Stretch)
                    {
                        return image;
                    }

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

        /// <summary>
        /// Returns the correct <see cref="Size"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        private Size ParseSize(string input)
        {
            const string Width = "width";
            const string Height = "height";
            Size size = new Size();

            // First merge the matches so we can parse .
            StringBuilder stringBuilder = new StringBuilder();
            foreach (Match match in SizeRegex.Matches(input))
            {
                stringBuilder.Append(match.Value);
            }

            // First cater for single dimensions.
            string value = stringBuilder.ToString();

            if (input.Contains(Width) && !input.Contains(Height))
            {
                size = new Size(value.ToPositiveIntegerArray()[0], 0);
            }

            if (input.Contains(Height) && !input.Contains(Width))
            {
                size = new Size(0, value.ToPositiveIntegerArray()[0]);
            }

            // Both dimensions supplied.
            if (input.Contains(Height) && input.Contains(Width))
            {
                int[] dimensions = value.ToPositiveIntegerArray();

                // Check the order in which they have been supplied.
                size = input.IndexOf(Width, StringComparison.Ordinal) < input.IndexOf(Height, StringComparison.Ordinal)
                    ? new Size(dimensions[0], dimensions[1])
                    : new Size(dimensions[1], dimensions[0]);
            }

            return size;
        }

        /// <summary>
        /// Returns the correct <see cref="ResizeMode"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="ResizeMode"/>.
        /// </returns>
        private ResizeMode ParseMode(string input)
        {
            foreach (Match match in ModeRegex.Matches(input))
            {
                // Split on =
                string mode = match.Value.Split('=')[1];

                switch (mode)
                {
                    case "stretch":
                        return ResizeMode.Stretch;
                    case "crop":
                        return ResizeMode.Crop;
                    case "max":
                        return ResizeMode.Max;
                    default:
                        return ResizeMode.Pad;
                }
            }

            return ResizeMode.Pad;
        }

        /// <summary>
        /// Returns the correct <see cref="AnchorPosition"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="AnchorPosition"/>.
        /// </returns>
        private AnchorPosition ParsePosition(string input)
        {
            foreach (Match match in AnchorRegex.Matches(input))
            {
                // Split on =
                string anchor = match.Value.Split('=')[1];

                switch (anchor)
                {
                    case "top":
                        return AnchorPosition.Top;
                    case "bottom":
                        return AnchorPosition.Bottom;
                    case "left":
                        return AnchorPosition.Left;
                    case "right":
                        return AnchorPosition.Right;
                    default:
                        return AnchorPosition.Center;
                }
            }

            return AnchorPosition.Center;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        private Color ParseColor(string input)
        {
            foreach (Match match in ColorRegex.Matches(input))
            {
                // Split on color-hex
                return ColorTranslator.FromHtml("#" + match.Value.Split('=')[1]);
            }

            return Color.Transparent;
        }
    }
}
