// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Quality.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the quality component of the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to change the quality component of the image.
    /// </summary>
    public class Quality : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"quality=[^&|,]+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Quality"/> class.
        /// </summary>
        public Quality()
        {
            this.Processor = new ImageProcessor.Processors.Quality();
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
                        int percentage = Math.Abs(CommonParameterParserUtility.ParseIn100Range(match.Value));
                        this.Processor.DynamicParameter = percentage;
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }
    }
}
