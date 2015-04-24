// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RotateBounded.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to rotate an image without expanding the canvas.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to rotate an image without expanding the canvas.
    /// </summary>
    public class RotateBounded : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"rotatebounded=[^&]", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search for.
        /// </summary>
        private static readonly Regex BoundRegex = new Regex(@"rotatebounded.keepsize=true", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateBounded"/> class.
        /// </summary>
        public RotateBounded()
        {
            this.Processor = new ImageProcessor.Processors.RotateBounded();
        }

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
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder
        {
            get;
            private set;
        }

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
                float angle = QueryParamParser.Instance.ParseValue<float>(queryCollection["rotatebounded"]);
                bool keepSize = BoundRegex.Match(queryString).Success;

                this.Processor.DynamicParameter = new Tuple<float, bool>(angle, keepSize);
            }

            return this.SortOrder;
        }
        #endregion
    }
}
