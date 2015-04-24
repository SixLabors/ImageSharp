// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Flip.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Flips an image horizontally or vertically.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Drawing;
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Flips an image horizontally or vertically.
    /// </summary>
    public class Flip : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"flip=(horizontal|vertical|both)", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Flip"/> class.
        /// </summary>
        public Flip()
        {
            this.Processor = new ImageProcessor.Processors.Flip();
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

                // We do not use the full enum so use switch.
                string direction = match.Value.Split('=')[1];

                switch (direction)
                {
                    case "horizontal":
                        this.Processor.DynamicParameter = RotateFlipType.RotateNoneFlipX;
                        break;
                    case "vertical":
                        this.Processor.DynamicParameter = RotateFlipType.RotateNoneFlipY;
                        break;
                    default:
                        this.Processor.DynamicParameter = RotateFlipType.RotateNoneFlipXY;
                        break;
                }
            }

            return this.SortOrder;
        }
    }
}