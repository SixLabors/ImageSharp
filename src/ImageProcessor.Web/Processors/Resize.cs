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
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(width|height)=|(width|height)ratio=|mode=(carve)?|anchor=|center=|upscale=", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the size attribute.
        /// </summary>
        private static readonly Regex SizeRegex = new Regex(@"(width|height)=\d+(.\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the ratio attribute.
        /// </summary>
        private static readonly Regex RatioRegex = new Regex(@"(width|height)ratio=\d+(.\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the mode attribute.
        /// </summary>
        private static readonly Regex ModeRegex = new Regex(@"mode=(pad|stretch|crop|max)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the anchor attribute.
        /// </summary>
        private static readonly Regex AnchorRegex = new Regex(@"anchor=(top|bottom|left|right|center)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the center attribute.
        /// </summary>
        private static readonly Regex CenterRegex = new Regex(@"center=\d+(.\d+)?[,-]\d+(.\d+)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the upscale attribute.
        /// </summary>
        private static readonly Regex UpscaleRegex = new Regex(@"upscale=false", RegexOptions.Compiled);

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
                    // We don't want any resize carve requests to interfere.
                    if (match.Value.ToUpperInvariant().Contains("CARVE"))
                    {
                        break;
                    }

                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;
                        stringBuilder.Append(queryString);
                    }

                    index += 1;
                }
            }

            // Match syntax
            string toParse = stringBuilder.ToString();

            Size size = this.ParseSize(toParse);
            ResizeLayer resizeLayer = new ResizeLayer(size)
            {
                ResizeMode = this.ParseMode(toParse),
                AnchorPosition = this.ParsePosition(toParse),
                Upscale = !UpscaleRegex.IsMatch(toParse),
                CenterCoordinates = this.ParseCoordinates(toParse),
            };

            this.Processor.DynamicParameter = resizeLayer;

            // Correctly parse any restrictions.
            string restrictions;
            this.Processor.Settings.TryGetValue("RestrictTo", out restrictions);
            ((ImageProcessor.Processors.Resize)this.Processor).RestrictedSizes = this.ParseRestrictions(restrictions);
            return this.SortOrder;
        }

        /// <summary>
        /// Returns the correct <see cref="Size"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        private Size ParseSize(string input)
        {
            const string Width = "width=";
            const string Height = "height=";
            const string WidthRatio = "widthratio=";
            const string HeightRatio = "heightratio=";
            Size size = new Size();

            // First merge the matches so we can parse .
            StringBuilder stringBuilder = new StringBuilder();
            foreach (Match match in SizeRegex.Matches(input))
            {
                stringBuilder.Append(match.Value);
            }

            // First cater for single dimensions.
            string value = stringBuilder.ToString();

            if (input.Contains(Width) && !input.Contains(Height))
            {
                size = new Size(Convert.ToInt32(value.ToPositiveFloatArray()[0]), 0);
            }

            if (input.Contains(Height) && !input.Contains(Width))
            {
                size = new Size(0, Convert.ToInt32(value.ToPositiveFloatArray()[0]));
            }

            // Both dimensions supplied.
            if (input.Contains(Height) && input.Contains(Width))
            {
                float[] dimensions = value.ToPositiveFloatArray();

                // Check the order in which they have been supplied.
                size = input.IndexOf(Width, StringComparison.Ordinal) < input.IndexOf(Height, StringComparison.Ordinal)
                    ? new Size(Convert.ToInt32(dimensions[0]), Convert.ToInt32(dimensions[1]))
                    : new Size(Convert.ToInt32(dimensions[1]), Convert.ToInt32(dimensions[0]));
            }

            // Calculate any ratio driven sizes.
            if (size.Width == 0 || size.Height == 0)
            {
                stringBuilder.Clear();
                foreach (Match match in RatioRegex.Matches(input))
                {
                    stringBuilder.Append(match.Value);
                }

                value = stringBuilder.ToString();

                // Replace 0 width
                if (size.Width == 0 && size.Height > 0 && input.Contains(WidthRatio) && !input.Contains(HeightRatio))
                {
                    size.Width = Convert.ToInt32(value.ToPositiveFloatArray()[0] * size.Height);
                }

                // Replace 0 height
                if (size.Height == 0 && size.Width > 0 && input.Contains(HeightRatio) && !input.Contains(WidthRatio))
                {
                    size.Height = Convert.ToInt32(value.ToPositiveFloatArray()[0] * size.Width);
                }
            }

            return size;
        }

        /// <summary>
        /// Returns the correct <see cref="ResizeMode"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="ResizeMode"/>.
        /// </returns>
        private ResizeMode ParseMode(string input)
        {
            foreach (Match match in ModeRegex.Matches(input))
            {
                // Split on =
                string mode = match.Value.Split('=')[1];

                switch (mode)
                {
                    case "stretch":
                        return ResizeMode.Stretch;
                    case "crop":
                        return ResizeMode.Crop;
                    case "max":
                        return ResizeMode.Max;
                    default:
                        return ResizeMode.Pad;
                }
            }

            return ResizeMode.Pad;
        }

        /// <summary>
        /// Returns the correct <see cref="AnchorPosition"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="AnchorPosition"/>.
        /// </returns>
        private AnchorPosition ParsePosition(string input)
        {
            foreach (Match match in AnchorRegex.Matches(input))
            {
                // Split on =
                string anchor = match.Value.Split('=')[1];

                switch (anchor)
                {
                    case "top":
                        return AnchorPosition.Top;
                    case "bottom":
                        return AnchorPosition.Bottom;
                    case "left":
                        return AnchorPosition.Left;
                    case "right":
                        return AnchorPosition.Right;
                    default:
                        return AnchorPosition.Center;
                }
            }

            return AnchorPosition.Center;
        }

        /// <summary>
        /// Parses the coordinates.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The <see cref="float"/> array containing the coordinates</returns>
        private float[] ParseCoordinates(string input)
        {
            float[] floats = { };

            foreach (Match match in CenterRegex.Matches(input))
            {
                floats = match.Value.ToPositiveFloatArray();
            }

            return floats;
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
                sizes.AddRange(input.Split(',').Select(this.ParseSize));
            }

            return sizes;
        }
    }
}
