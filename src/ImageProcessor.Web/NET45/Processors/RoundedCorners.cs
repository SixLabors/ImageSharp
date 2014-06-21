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
    using System.Globalization;
    using System.Text.RegularExpressions;
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
        private static readonly Regex QueryRegex = new Regex(@"roundedcorners=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the angle attribute.
        /// </summary>
        private static readonly Regex RadiusRegex = new Regex(@"(roundedcorners|radius)(=|-)(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the top left attribute.
        /// </summary>
        private static readonly Regex TopLeftRegex = new Regex(@"tl(=|-)(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the top right attribute.
        /// </summary>
        private static readonly Regex TopRightRegex = new Regex(@"tr(=|-)(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the bottom left attribute.
        /// </summary>
        private static readonly Regex BottomLeftRegex = new Regex(@"bl(=|-)(true|false)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the bottom right attribute.
        /// </summary>
        private static readonly Regex BottomRightRegex = new Regex(@"br(=|-)(true|false)", RegexOptions.Compiled);

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

                        RoundedCornerLayer roundedCornerLayer = new RoundedCornerLayer(
                                this.ParseRadius(queryString), 
                                CommonParameterParserUtility.ParseColor(queryString), 
                                this.ParseCorner(TopLeftRegex, queryString),
                                this.ParseCorner(TopRightRegex, queryString),
                                this.ParseCorner(BottomLeftRegex, queryString),
                                this.ParseCorner(BottomRightRegex, queryString));

                        this.Processor.DynamicParameter = roundedCornerLayer;
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }

        #region Private Methods
        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the radius for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the radius for the given string.
        /// </returns>
        private int ParseRadius(string input)
        {
            foreach (Match match in RadiusRegex.Matches(input))
            {
                // Split on radius-
                int radius;
                int.TryParse(match.Value.Split(new[] { '=', '-' })[1], NumberStyles.Any, CultureInfo.InvariantCulture, out radius);
                return radius;
            }

            // No corners - matches the RoundedCorner default.
            return 0;
        }

        /// <summary>
        /// Returns a <see cref="T:System.Boolean"/> either true or false.
        /// </summary>
        /// <param name="corner">
        /// The corner.
        /// </param>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Boolean"/> true or false.
        /// </returns>
        private bool ParseCorner(Regex corner, string input)
        {
            foreach (Match match in corner.Matches(input))
            {
                // Split on corner-
                bool cornerRound;
                bool.TryParse(match.Value.Split(new[] { '=', '-' })[1], out cornerRound);
                return cornerRound;
            }

            // No corners - matches the RoundedCorner default.
            return true;
        }
        #endregion
    }
}
