// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Hue.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to adjust the hue component of an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Processors;

    /// <summary>
    /// Encapsulates methods to adjust the hue component of an image.
    /// </summary>
    public class Hue : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(hue=|hue.\w+=)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The hue regex.
        /// </summary>
        private static readonly Regex HueRegex = new Regex(@"hue=\d+", RegexOptions.Compiled);

        /// <summary>
        /// The rotate regex.
        /// </summary>
        private static readonly Regex RotateRegex = new Regex(@"hue.rotate=true", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Hue"/> class.
        /// </summary>
        public Hue()
        {
            this.Processor = new ImageProcessor.Processors.Hue();
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
                int degrees = this.ParseDegrees(toParse);
                bool rotate = RotateRegex.Match(toParse).Success;
                this.Processor.DynamicParameter = new Tuple<int, bool>(degrees, rotate);
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
        public int ParseDegrees(string input)
        {
            int degrees = 0;

            foreach (Match match in HueRegex.Matches(input))
            {
                degrees = int.Parse(match.Value.Split('=')[1], CultureInfo.InvariantCulture);
            }

            return Math.Max(0, Math.Min(360, degrees));
        }
    }
}
