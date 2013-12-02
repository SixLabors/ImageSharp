// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Resize.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Resizes an image to the given dimensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;

    #endregion

    /// <summary>
    /// Resizes an image to the given dimensions.
    /// </summary>
    public class Resize : ResizeBase
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"((width|height)=\d+)|(mode=(pad|stretch|crop|constrain))|(anchor=(top|bottom|left|right|center))|(bgcolor=([0-9a-fA-F]{3}){1,2})", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the size attribute.
        /// </summary>
        private static readonly Regex SizeRegex = new Regex(@"(width|height)=\d+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the mode attribute.
        /// </summary>
        private static readonly Regex ModeRegex = new Regex(@"mode=(pad|stretch|crop|constrain)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the anchor attribute.
        /// </summary>
        private static readonly Regex AnchorRegex = new Regex(@"anchor=(top|bottom|left|right|center)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the color attribute.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"bgcolor=([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public override Regex RegexPattern
        {
            get
            {
                return QueryRegex;
            }
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public override dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public override int SortOrder
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public override Dictionary<string, string> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public override int MatchRegexIndex(string queryString)
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

            // Match syntax
            string toParse = stringBuilder.ToString();

            Size size = this.ParseSize(toParse);
            ResizeLayer resizeLayer = new ResizeLayer(size)
                                          {
                                              ResizeMode = this.ParseMode(toParse),
                                              AnchorPosition = this.ParsePosition(toParse),
                                              BackgroundColor = this.ParseColor(toParse)
                                          };

            this.DynamicParameter = resizeLayer;
            return this.SortOrder;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public override Image ProcessImage(ImageFactory factory)
        {
            int width = this.DynamicParameter.Size.Width ?? 0;
            int height = this.DynamicParameter.Size.Height ?? 0;
            ResizeMode mode = this.DynamicParameter.ResizeMode;
            AnchorPosition anchor = this.DynamicParameter.AnchorPosition;
            Color backgroundColor = this.DynamicParameter.BackgroundColor;

            int defaultMaxWidth;
            int defaultMaxHeight;
            int.TryParse(this.Settings["MaxWidth"], out defaultMaxWidth);
            int.TryParse(this.Settings["MaxHeight"], out defaultMaxHeight);

            if (mode == ResizeMode.Constrain)
            {
                // Just use the old constrain plugin to handle the resize.
                var constrainSettings = new Dictionary<string, string> { { "MaxWidth", defaultMaxWidth.ToString("G") }, { "MaxHeight", defaultMaxHeight.ToString("G") } };
                Constrain constrain = new Constrain { DynamicParameter = new Size(width, height), Settings = constrainSettings };
                return constrain.ProcessImage(factory);
            }

            return this.ResizeImage(factory, width, height, defaultMaxWidth, defaultMaxHeight, backgroundColor, mode, anchor);
        }
        #endregion

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
            const string Width = "width";
            const string Height = "height";
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
                size = new Size(value.ToPositiveIntegerArray()[0], 0);
            }

            if (input.Contains(Height) && !input.Contains(Width))
            {
                size = new Size(0, value.ToPositiveIntegerArray()[0]);
            }

            // Both dimensions supplied.
            if (input.Contains(Height) && input.Contains(Width))
            {
                int[] dimensions = value.ToPositiveIntegerArray();

                // Check the order in which they have been supplied.
                size = input.IndexOf(Width, StringComparison.Ordinal) < input.IndexOf(Height, StringComparison.Ordinal)
                    ? new Size(dimensions[0], dimensions[1])
                    : new Size(dimensions[1], dimensions[0]);
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
                    case "constrain":
                        return ResizeMode.Constrain;
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
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        private Color ParseColor(string input)
        {
            foreach (Match match in ColorRegex.Matches(input))
            {
                // Split on color-hex
                return ColorTranslator.FromHtml("#" + match.Value.Split('=')[1]);
            }

            return Color.Transparent;
        }
    }
}
