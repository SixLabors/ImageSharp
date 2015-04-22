// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Resize.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Resizes an image to the given dimensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(width|height)=((.)?\d+|\d+(.\d+)?)+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Resize"/> class.
        /// </summary>
        public Resize()
        {
            this.Processor = new ImageProcessor.Processors.Resize();
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
                Size size = this.ParseSize(queryCollection);
                ResizeMode mode = QueryParamParser.Instance.ParseValue<ResizeMode>(queryCollection["mode"]);
                AnchorPosition position = QueryParamParser.Instance.ParseValue<AnchorPosition>(queryCollection["anchor"]);
                bool upscale = queryCollection["upscale"] == null || QueryParamParser.Instance.ParseValue<bool>(queryCollection["upscale"]);
                float[] center = queryCollection["center"] != null
                                    ? QueryParamParser.Instance.ParseValue<float[]>(queryCollection["center"]) :
                                    new float[] { };

                ResizeLayer resizeLayer = new ResizeLayer(size)
                {
                    ResizeMode = mode,
                    AnchorPosition = position,
                    Upscale = upscale,
                    CenterCoordinates = center
                };

                this.Processor.DynamicParameter = resizeLayer;

                // Correctly parse any restrictions.
                string restrictions;
                this.Processor.Settings.TryGetValue("RestrictTo", out restrictions);
                ((ImageProcessor.Processors.Resize)this.Processor).RestrictedSizes = this.ParseRestrictions(
                    restrictions);
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Returns the correct <see cref="Size"/> for the given query collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> containing the query parameters to parse.
        /// </param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        private Size ParseSize(NameValueCollection queryCollection)
        {
            string width = queryCollection["width"];
            string height = queryCollection["height"];
            string widthRatio = queryCollection["widthratio"];
            string heightRatio = queryCollection["heightratio"];
            Size size = new Size();

            // Umbraco calls the API incorrectly so we have to deal with floats.
            // We round up so that single pixel lines are not produced.
            const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

            // First cater for single dimensions.
            if (width != null && height == null)
            {
                size = new Size((int)Math.Round(QueryParamParser.Instance.ParseValue<float>(width), Rounding), 0);
            }

            if (width == null && height != null)
            {
                size = new Size(0, (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(height), Rounding));
            }

            // Both supplied
            if (width != null && height != null)
            {
                size = new Size(
                    (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(width), Rounding),
                    (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(height), Rounding));
            }

            // Calculate any ratio driven sizes.
            if (size.Width == 0 || size.Height == 0)
            {
                // Replace 0 width
                if (size.Width == 0 && size.Height > 0 && widthRatio != null && heightRatio == null)
                {
                    size.Width = (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(widthRatio) * size.Height, Rounding);
                }

                // Replace 0 height
                if (size.Width > 0 && size.Height == 0 && widthRatio == null && heightRatio != null)
                {
                    size.Height = (int)Math.Round(QueryParamParser.Instance.ParseValue<float>(heightRatio) * size.Width, Rounding);
                }
            }

            return size;
        }

        /// <summary>
        /// Returns a <see cref="List{T}"/> of sizes to restrict resizing to.
        /// </summary>
        /// <param name="input">
        /// The input.
        /// </param>
        /// <returns>
        /// The <see cref="List{Size}"/> to restrict resizing to.
        /// </returns>
        private List<Size> ParseRestrictions(string input)
        {
            List<Size> sizes = new List<Size>();

            if (!string.IsNullOrWhiteSpace(input))
            {
                sizes.AddRange(input.Split(',').Select(q => this.ParseSize(HttpUtility.ParseQueryString(q))));
            }

            return sizes;
        }
    }
}
