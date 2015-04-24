// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BackgroundColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Changes the background color of an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Changes the background color of an image.
    /// </summary>
    public class BackgroundColor : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"bgcolor=[^&]", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColor"/> class.
        /// </summary>
        public BackgroundColor()
        {
            this.Processor = new ImageProcessor.Processors.BackgroundColor();
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
                Color color = QueryParamParser.Instance.ParseValue<Color>(queryCollection["bgcolor"]);
                this.Processor.DynamicParameter = color;
            }

            return this.SortOrder;
        }
    }
}