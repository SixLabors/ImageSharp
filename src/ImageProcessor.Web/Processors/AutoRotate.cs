// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AutoRotate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs auto-rotation to ensure that EXIF defined rotation is reflected in
//   the final image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Performs auto-rotation to ensure that EXIF defined rotation is reflected in 
    /// the final image.
    /// </summary>
    public class AutoRotate : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"autorotate=true", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoRotate"/> class.
        /// </summary>
        public AutoRotate()
        {
            this.Processor = new ImageProcessor.Processors.AutoRotate();
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
            }

            return this.SortOrder;
        }
    }
}