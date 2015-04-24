// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RoundedCorners.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to add rounded corners to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to add rounded corners to an image.
    /// </summary>
    public class RoundedCorners : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"roundedcorners=\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="RoundedCorners"/> class.
        /// </summary>
        public RoundedCorners()
        {
            this.Processor = new ImageProcessor.Processors.RoundedCorners();
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

                RoundedCornerLayer roundedCornerLayer = new RoundedCornerLayer(
                    QueryParamParser.Instance.ParseValue<int>(queryCollection["roundedcorners"]),
                    this.ParseCorner(queryCollection, "tl"),
                    this.ParseCorner(queryCollection, "tr"),
                    this.ParseCorner(queryCollection, "bl"),
                    this.ParseCorner(queryCollection, "br"));

                this.Processor.DynamicParameter = roundedCornerLayer;
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Returns a value indicating whether to round the given corner.
        /// </summary>
        /// <param name="queryCollection">
        /// The collection of query parameters.
        /// </param>
        /// <param name="key">
        /// The parameter key.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Boolean"/> true or false.
        /// </returns>
        private bool ParseCorner(NameValueCollection queryCollection, string key)
        {
            return queryCollection[key] == null || QueryParamParser.Instance.ParseValue<bool>(queryCollection[key]);
        }
    }
}
