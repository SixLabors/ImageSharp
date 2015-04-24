// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Vignette.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a vignette image effect to an image.
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
    /// Encapsulates methods with which to add a vignette image effect to an image.
    /// </summary>
    public class Vignette : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"vignette(=true)?[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Vignette"/> class.
        /// </summary>
        public Vignette()
        {
            this.Processor = new ImageProcessor.Processors.Vignette();
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
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
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
                string vignette = queryCollection["vignette"];
                bool doVignette = QueryParamParser.Instance.ParseValue<bool>(vignette);
                Color color = doVignette
                    ? Color.Black
                    : QueryParamParser.Instance.ParseValue<Color>(vignette);

                this.Processor.DynamicParameter = color;
            }

            return this.SortOrder;
        }
    }
}