// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Pixelate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to pixelate an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to pixelate an image.
    /// </summary>
    public class Pixelate : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"pixelate=[^&]", RegexOptions.Compiled);

        /// <summary>
        /// The pixel regex.
        /// </summary>
        private static readonly Regex PixelRegex = new Regex(@"pixelate=\d+", RegexOptions.Compiled);

        /// <summary>
        /// The rectangle regex.
        /// </summary>
        private static readonly Regex RectangleRegex = new Regex(@"pixelrect=\d+,\d+,\d+,\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Pixelate"/> class.
        /// </summary>
        public Pixelate()
        {
            this.Processor = new ImageProcessor.Processors.Pixelate();
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
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder { get; private set; }

        /// <summary>
        /// Gets the associated graphics processor.
        /// </summary>
        public IGraphicsProcessor Processor { get; private set; }

        /// <summary>
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">The query string to search.</param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public int MatchRegexIndex(string queryString)
        {
            this.SortOrder = int.MaxValue;
            Match match = this.RegexPattern.Match(queryString);

            if (match.Success)
            {
                this.SortOrder = match.Index;
                NameValueCollection queryCollection = HttpUtility.ParseQueryString(queryString);
                int size = QueryParamParser.Instance.ParseValue<int>(queryCollection["pixelate"]);

                Rectangle? rectangle = queryCollection["pixelrect"] != null
                      ? QueryParamParser.Instance.ParseValue<Rectangle>(queryCollection["pixelrect"])
                      : (Rectangle?)null; 

                this.Processor.DynamicParameter = new Tuple<int, Rectangle?>(size, rectangle);
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Returns the correct size of pixels.
        /// </summary>
        /// <param name="input">
        /// The input containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="int"/> representing the pixel size.
        /// </returns>
        public int ParseSize(string input)
        {
            int size = 0;

            foreach (Match match in PixelRegex.Matches(input))
            {
                size = int.Parse(match.Value.Split('=')[1], CultureInfo.InvariantCulture);
            }

            return size;
        }

        /// <summary>
        /// Returns the correct <see cref="Nullable{Rectange}"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="Nullable{Rectange}"/>
        /// </returns>
        private Rectangle? ParseRectangle(string input)
        {
            int[] dimensions = { };

            foreach (Match match in RectangleRegex.Matches(input))
            {
                dimensions = match.Value.ToPositiveIntegerArray();
            }

            if (dimensions.Length == 4)
            {
                return new Rectangle(dimensions[0], dimensions[1], dimensions[2], dimensions[3]);
            }

            return null;
        }
    }
}
