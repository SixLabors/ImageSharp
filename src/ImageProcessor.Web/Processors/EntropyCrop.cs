// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EntropyCrop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs a crop on an image to the area of greatest entropy.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Globalization;
    using System.Text.RegularExpressions;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Performs a crop on an image to the area of greatest entropy.
    /// </summary>
    public class EntropyCrop : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"entropycrop=(\d+)[^&]+|entropycrop", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCrop"/> class.
        /// </summary>
        public EntropyCrop()
        {
            this.Processor = new ImageProcessor.Processors.EntropyCrop();
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
                        byte threshold = this.ParseThreshold(match.Value);
                        this.Processor.DynamicParameter = threshold;
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
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
        private byte ParseThreshold(string input)
        {
            foreach (Match match in QueryRegex.Matches(input))
            {
                if (!match.Value.Contains("="))
                {
                    continue;
                }

                // Split on threshold
                int threshold;
                int.TryParse(match.Value.Split('=')[1], NumberStyles.Any, CultureInfo.InvariantCulture, out threshold);
                return threshold.ToByte();
            }

            // No threshold - matches the EntropyCrop default.
            return 128;
        }
    }
}
