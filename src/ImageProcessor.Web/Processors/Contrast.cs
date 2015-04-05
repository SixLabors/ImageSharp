// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Contrast.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the contrast component of the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Imaging.Helpers;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to change the contrast component of the image.
    /// </summary>
    public class Contrast : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"contrast=(-)?\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Contrast"/> class.
        /// </summary>
        public Contrast()
        {
            this.Processor = new ImageProcessor.Processors.Contrast();
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
                int percentage = QueryParamParser.Instance.ParseValue<int>(queryCollection["contrast"]);
                percentage = ImageMaths.Clamp(percentage, -100, 100);
                this.Processor.DynamicParameter = percentage;
            }

            return this.SortOrder;
        }
    }
}