// -----------------------------------------------------------------------
// <copyright file="Resize.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
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
    public class Resize : ResizeBase
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(width|height)=\d+", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public override Regex RegexPattern
        {
            get
            {
                return QueryRegex;
            }
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public override dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public override int SortOrder
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public override Dictionary<string, string> Settings
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
        public override int MatchRegexIndex(string queryString)
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
                    
                    // Match syntax
                    if (match.Value.Contains("width"))
                    {
                        size.Width = match.Value.ToPositiveIntegerArray()[0];
                    }
                    else
                    {
                        size.Height = match.Value.ToPositiveIntegerArray()[0];
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
        public override Image ProcessImage(ImageFactory factory)
        {

			int width = this.DynamicParameter.Width ?? 0;
			int height = this.DynamicParameter.Height ?? 0;

			int defaultMaxWidth;
			int defaultMaxHeight;
			int.TryParse(this.Settings["MaxWidth"], out defaultMaxWidth);
			int.TryParse(this.Settings["MaxHeight"], out defaultMaxHeight);

			return ResizeImage(factory, width, height, defaultMaxWidth, defaultMaxHeight);
        }

	    #endregion
    }
}
