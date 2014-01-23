// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Vignette.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a vignette image effect to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add a vignette image effect to an image.
    /// </summary>
    public class Vignette : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"vignette=true", RegexOptions.Compiled);

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
                        bool doVignette;
                        this.DynamicParameter = bool.TryParse(match.Value.Split('=')[1], out doVignette);
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
                newImage = new Bitmap(image);

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    Rectangle bounds = new Rectangle(0, 0, newImage.Width, newImage.Height);
                    Rectangle ellipsebounds = bounds;

                    // Increase the rectangle size by the difference between the rectangle dimensions and sqrt(2)/2 * the rectangle dimensions.
                    // Why sqrt(2)/2? Because the point (sqrt(2)/2, sqrt(2)/2) is the 45 degree angle point on a unit circle. Scaling by the width 
                    // and height gives the distance needed to inflate the rect to make sure it's fully covered.
                    ellipsebounds.Offset(-ellipsebounds.X, -ellipsebounds.Y);
                    int x = ellipsebounds.Width - (int)Math.Floor(.70712 * ellipsebounds.Width);
                    int y = ellipsebounds.Height - (int)Math.Floor(.70712 * ellipsebounds.Height);
                    ellipsebounds.Inflate(x, y);

                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(ellipsebounds);
                        using (PathGradientBrush brush = new PathGradientBrush(path))
                        {
                            // Fill a rectangle with an elliptical gradient brush that goes from white to black.
                            // Change the colour from white to pure transparent black and from black to pure opaque black. 
                            // This has the effect of painting the far corners black and shade less on the way in to the centre.
                            brush.WrapMode = WrapMode.Tile;
                            brush.CenterColor = Color.FromArgb(0, 0, 0, 0);
                            brush.SurroundColors = new[] { Color.FromArgb(255, 0, 0, 0) };

                            Blend blend = new Blend
                                {
                                    Positions = new[] { 0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0F },
                                    Factors = new[] { 0.0f, 0.5f, 1f, 1f, 1.0f, 1.0f }
                                };

                            brush.Blend = blend;

                            Region oldClip = graphics.Clip;
                            graphics.Clip = new Region(bounds);
                            graphics.FillRectangle(brush, ellipsebounds);
                            graphics.Clip = oldClip;
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