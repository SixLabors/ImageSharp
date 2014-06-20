// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GaussianBlur.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Applies a Gaussian blur to the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Globalization;
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// </summary>
    public class GaussianBlur : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"blur=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianBlur"/> class.
        /// </summary>
        public GaussianBlur()
        {
            this.Processor = new ImageProcessor.Processors.GaussianBlur();
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

                        // Normalise and set the variables.
                        int maxSize;
                        double maxSigma;
                        int maxThreshold;

                        int.TryParse(this.Processor.Settings["MaxSize"], NumberStyles.Any, CultureInfo.InvariantCulture, out maxSize);
                        double.TryParse(this.Processor.Settings["MaxSigma"], NumberStyles.Any, CultureInfo.InvariantCulture, out maxSigma);
                        int.TryParse(this.Processor.Settings["MaxThreshold"], NumberStyles.Any, CultureInfo.InvariantCulture, out maxThreshold);
                        
                        this.Processor.DynamicParameter = CommonParameterParserUtility.ParseGaussianLayer(queryString, maxSize, maxSigma, maxThreshold);
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }
    }
}