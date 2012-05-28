// -----------------------------------------------------------------------
// <copyright file="Resize.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    #endregion

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(width|height)=\d+", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                return "Resize";
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                return "Resizes an image to the given dimensions.";
            }
        }

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
                Size size = new Size();

                foreach (Match match in this.RegexPattern.Matches(queryString))
                {
                    if (match.Success)
                    {
                        if (index == 0)
                        {
                            // Set the index on the first instance only.
                            this.SortOrder = match.Index;
                        }

                        if (match.Value.Contains("width"))
                        {
                            size.Width = match.Value.ToIntegerArray()[0];
                        }
                        else
                        {
                            size.Height = match.Value.ToIntegerArray()[0];
                        }

                        index += 1;
                    }
                }

                this.DynamicParameter = size;
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
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                int width = this.DynamicParameter.Width ?? 0;
                int height = this.DynamicParameter.Height ?? 0;
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                int defaultMaxWidth;
                int defaultMaxHeight;
                int.TryParse(this.Settings["MaxWidth"], out defaultMaxWidth);
                int.TryParse(this.Settings["MaxHeight"], out defaultMaxHeight);
                int maxWidth = defaultMaxWidth > 0 ? defaultMaxWidth : int.MaxValue;
                int maxHeight = defaultMaxHeight > 0 ? defaultMaxHeight : int.MaxValue;

                // If height or width is not passed we assume that the standard ratio is to be kept.
                if (height == 0)
                {
                    // Bit of simple fractional maths here.
                    float percentWidth = Math.Abs(width / (float)sourceWidth);
                    height = (int)Math.Floor(sourceHeight * percentWidth);
                }

                if (width == 0)
                {
                    float percentHeight = Math.Abs(height / (float)sourceHeight);
                    width = (int)Math.Floor(sourceWidth * percentHeight);
                }

                if (width > 0 && height > 0 && width <= maxWidth && height <= maxHeight)
                {
                    newImage = new Bitmap(width, height, PixelFormat.Format32bppPArgb) { Tag = image.Tag };

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        // We want to use two different blending algorithms for enlargement/shrinking.
                        // Bicubic is better enlarging for whilst Bilinear is better for shrinking.
                        // http://www.codinghorror.com/blog/2007/07/better-image-resizing.html
                        if (image.Width < width && image.Height < height)
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

                            // Contrary to everything I have read bicubic is producing the best results.
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.None;
                            graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        }

                        // An unwanted border appears when using InterpolationMode.HighQualityBicubic to resize the image
                        // as the algorithm appears to be pulling averaging detail from surFlooring pixels beyond the edge 
                        // of the image. Using the ImageAttributes class to specify that the pixels beyond are simply mirror 
                        // images of the pixels within solves this problem.
                        using (ImageAttributes wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            Rectangle destRect = new Rectangle(0, 0, width, height);
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
