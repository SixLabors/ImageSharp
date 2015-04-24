// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Crop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Crops an image to the given directions.
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
    /// Crops an image to the given directions.
    /// </summary>
    public class Crop : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"\b(?!entropy)crop\b[=]", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Crop"/> class.
        /// </summary>
        public Crop()
        {
            this.Processor = new ImageProcessor.Processors.Crop();
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
                float[] coordinates = QueryParamParser.Instance.ParseValue<float[]>(queryCollection["crop"]);

                // Default CropMode.Pixels will be returned.
                CropMode cropMode = QueryParamParser.Instance.ParseValue<CropMode>(queryCollection["cropmode"]);
                CropLayer cropLayer = new CropLayer(coordinates[0], coordinates[1], coordinates[2], coordinates[3], cropMode);
                this.Processor.DynamicParameter = cropLayer;
            }

            return this.SortOrder;
        }
    }
}
