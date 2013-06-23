// -----------------------------------------------------------------------
// <copyright file="RoundedCorners.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates methods to add rounded corners to an image.
    /// </summary>
    public class RoundedCorners : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"roundedcorners=(\d+|[^&]*)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the angle attribute.
        /// </summary>
        private static readonly Regex RadiusRegex = new Regex(@"radius-(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the color attribute.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"bgcolor-([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the top left attribute.
        /// </summary>
        private static readonly Regex TopLeftRegex = new Regex(@"tl-(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the top right attribute.
        /// </summary>
        private static readonly Regex TopRightRegex = new Regex(@"tr-(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the bottom left attribute.
        /// </summary>
        private static readonly Regex BottomLeftRegex = new Regex(@"bl-(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the bottom right attribute.
        /// </summary>
        private static readonly Regex BottomRightRegex = new Regex(@"br-(true|false)", RegexOptions.Compiled);

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

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;

                        RoundedCornerLayer roundedCornerLayer;

                        string toParse = match.Value;

                        if (toParse.Contains("bgcolor"))
                        {
                            roundedCornerLayer = new RoundedCornerLayer(this.ParseRadius(toParse), this.ParseColor(toParse), this.ParseCorner(TopLeftRegex, toParse), this.ParseCorner(TopRightRegex, toParse), this.ParseCorner(BottomLeftRegex, toParse), this.ParseCorner(BottomRightRegex, toParse));
                        }
                        else
                        {
                            int radius;
                            int.TryParse(match.Value.Split('=')[1], out radius);

                            roundedCornerLayer = new RoundedCornerLayer(radius, this.ParseCorner(TopLeftRegex, toParse), this.ParseCorner(TopRightRegex, toParse), this.ParseCorner(BottomLeftRegex, toParse), this.ParseCorner(BottomRightRegex, toParse));
                        }

                        this.DynamicParameter = roundedCornerLayer;
                    }

                    index += 1;
                }
            }

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
                RoundedCornerLayer roundedCornerLayer = this.DynamicParameter;
                int radius = roundedCornerLayer.Radius;
                Color backgroundColor = roundedCornerLayer.BackgroundColor;
                bool topLeft = roundedCornerLayer.TopLeft;
                bool topRight = roundedCornerLayer.TopRight;
                bool bottomLeft = roundedCornerLayer.BottomLeft;
                bool bottomRight = roundedCornerLayer.BottomRight;

                // Create a rotated image.
                newImage = this.RoundCornerImage(image, radius, backgroundColor, topLeft, topRight, bottomLeft, bottomRight);
                newImage.Tag = image.Tag;

                image.Dispose();
                image = newImage;
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

        #region Private Methods
        /// <summary>
        /// Adds rounded corners to the image
        /// </summary>
        /// <param name="image">The image to add corners too</param>
        /// <param name="cornerRadius">The radius of the corners.</param>
        /// <param name="backgroundColor">The background color to fill an image with.</param>
        /// <param name="topLeft">If the top left corner will have a rounded corner?</param>
        /// <param name="topRight">If the top right corner will have a rounded corner?</param>
        /// <param name="bottomLeft">If the bottom left corner will have a rounded corner?</param>
        /// <param name="bottomRight">If the bottom right corner will have a rounded corner?</param>
        /// <returns>The image with rounded corners.</returns>
        private Bitmap RoundCornerImage(Image image, int cornerRadius, Color backgroundColor, bool topLeft = false, bool topRight = false, bool bottomLeft = false, bool bottomRight = false)
        {
            int width = image.Width;
            int height = image.Height;

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(image.Width, image.Height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                // Contrary to everything I have read bicubic is producing the best results.
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;

                // Fill the background.
                graphics.Clear(backgroundColor);

                // Add rounded corners
                using (GraphicsPath path = new GraphicsPath())
                {
                    // Determined if the top left has a rounded corner
                    if (topLeft)
                    {
                        path.AddArc(0, 0, cornerRadius, cornerRadius, 180, 90);
                    }
                    else
                    {
                        path.AddLine(0, 0, 0, 0);
                    }

                    // Determined if the top right has a rounded corner
                    if (topRight)
                    {
                        path.AddArc(0 + width - cornerRadius, 0, cornerRadius, cornerRadius, 270, 90);
                    }
                    else
                    {
                        path.AddLine(width, 0, width, 0);
                    }

                    // Determined if the bottom left has a rounded corner
                    if (bottomRight)
                    {
                        path.AddArc(0 + width - cornerRadius, 0 + height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                    }
                    else
                    {
                        path.AddLine(width, height, width, height);
                    }

                    // Determined if the bottom right has a rounded corner
                    if (bottomLeft)
                    {
                        path.AddArc(0, 0 + height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                    }
                    else
                    {
                        path.AddLine(0, height, 0, height);
                    }

                    using (Brush brush = new TextureBrush(image))
                    {
                        graphics.FillPath(brush, path);
                    }
                }
            }

            return newImage;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the radius for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the radius for the given string.
        /// </returns>
        private int ParseRadius(string input)
        {
            foreach (Match match in RadiusRegex.Matches(input))
            {
                // Split on radius-
                int radius;
                int.TryParse(match.Value.Split('-')[1], out radius);
                return radius;
            }

            // No rotate - matches the RotateLayer default.
            return 0;
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
                // split on color-hex
                return ColorTranslator.FromHtml("#" + match.Value.Split('-')[1]);
            }

            return Color.Transparent;
        }

        /// <summary>
        /// Returns a <see cref="T:System.Boolean"/> either true or false.
        /// </summary>
        /// <param name="corner">
        /// The corner.
        /// </param>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Boolean"/> true or false.
        /// </returns>
        private bool ParseCorner(Regex corner, string input)
        {
            foreach (Match match in corner.Matches(input))
            {
                // Split on corner-
                bool cornerRound;
                bool.TryParse(match.Value.Split('-')[1], out cornerRound);
                return cornerRound;
            }

            // No rotate - matches the RotateLayer default.
            return true;
        }
        #endregion
    }
}
