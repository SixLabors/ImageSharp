// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReplaceColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods allowing the replacement of a color within an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods allowing the replacement of a color within an image.
    /// </summary>
    public class ReplaceColor : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(replace=|fuzziness=)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The replace regex.
        /// </summary>
        private static readonly Regex ReplaceRegex = new Regex(@"replace=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The fuzz regex.
        /// </summary>
        private static readonly Regex FuzzRegex = new Regex(@"fuzziness=\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceColor"/> class.
        /// </summary>
        public ReplaceColor()
        {
            this.Processor = new ImageProcessor.Processors.ReplaceColor();
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

            // First merge the matches so we can parse .
            StringBuilder stringBuilder = new StringBuilder();

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;
                    }

                    stringBuilder.Append(match.Value);

                    index += 1;
                }
            }

            if (this.SortOrder < int.MaxValue)
            {
                // Match syntax
                string toParse = stringBuilder.ToString();
                Color[] colors = this.ParseColor(toParse);
                int fuzziness = this.ParseFuzziness(toParse);
                this.Processor.DynamicParameter = new Tuple<Color, Color, int>(colors[0], colors[1], fuzziness);
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Returns the angle to alter the hue.
        /// </summary>
        /// <param name="input">
        /// The input containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="int"/> representing the angle.
        /// </returns>
        public Color[] ParseColor(string input)
        {
            IEnumerable<Color> colors = Enumerable.Empty<Color>();
            Match match = ReplaceRegex.Match(input);
            if (match.Success)
            {
                string[] colorQuery = match.Value.Split('=')[1].Split('|');
                colors = colorQuery.Select(CommonParameterParserUtility.ParseColor);
            }

            return colors.ToArray();
        }

        /// <summary>
        /// Returns the angle to alter the hue.
        /// </summary>
        /// <param name="input">
        /// The input containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="int"/> representing the angle.
        /// </returns>
        public int ParseFuzziness(string input)
        {
            int fuzziness = 0;

            Match match = FuzzRegex.Match(input);
            if (match.Success)
            {
                fuzziness = int.Parse(match.Value.Split('=')[1], CultureInfo.InvariantCulture);
            }

            return Math.Max(0, Math.Min(128, fuzziness));
        }
    }
}
