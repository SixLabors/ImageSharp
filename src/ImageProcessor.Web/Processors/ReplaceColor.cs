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
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Imaging.Helpers;
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
        private static readonly Regex QueryRegex = new Regex(@"replace=[^&]", RegexOptions.Compiled);

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
            this.SortOrder = int.MaxValue;
            Match match = this.RegexPattern.Match(queryString);

            if (match.Success)
            {
                this.SortOrder = match.Index;
                NameValueCollection queryCollection = HttpUtility.ParseQueryString(queryString);
                Color[] colors = QueryParamParser.Instance.ParseValue<Color[]>(queryCollection["replace"]);
                int fuzziness = QueryParamParser.Instance.ParseValue<int>(queryCollection["fuzziness"]);

                fuzziness = ImageMaths.Clamp(fuzziness, 0, 128);
                this.Processor.DynamicParameter = new Tuple<Color, Color, int>(colors[0], colors[1], fuzziness);
            }

            return this.SortOrder;
        }
    }
}
