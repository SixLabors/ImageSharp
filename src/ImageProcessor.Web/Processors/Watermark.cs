// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Watermark.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to add a watermark text overlay to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Drawing;
    using System.Drawing.Text;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to add a watermark text overlay to an image.
    /// </summary>
    public class Watermark : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"watermark=[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the text attribute.
        /// </summary>
        private static readonly Regex TextRegex = new Regex(@"(watermark=[^text-]|text-)[^/:?#\[\]@!$&'()*%\|,;=&]+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the position attribute.
        /// </summary>
        private static readonly Regex PositionRegex = new Regex(@"(watermark.position|textposition|[^.](&,=)?position)(=|-)\d+[-,]\d+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the font size attribute.
        /// </summary>
        private static readonly Regex FontSizeRegex = new Regex(@"((font)?)size(=|-)\d{1,3}", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the font style attribute.
        /// </summary>
        private static readonly Regex FontStyleRegex = new Regex(@"((font)?)style(=|-)(bold|italic|regular|strikeout|underline)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the font family attribute.
        /// </summary>
        private static readonly Regex FontFamilyRegex = new Regex(@"font(family)?(=|-)[^/:?#\[\]@!$&'()*%\|,;=0-9]+", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the opacity attribute.
        /// </summary>
        private static readonly Regex OpacityRegex = new Regex(@"(watermark.opacity|fontopacity|[^.](&,=)?opacity)(=|-)(?:100|[1-9]?[0-9])", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the shadow attribute.
        /// </summary>
        private static readonly Regex ShadowRegex = new Regex(@"((text|font|drop)?)shadow(=|-)true", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the color attribute.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"color(=|-)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Watermark"/> class.
        /// </summary>
        public Watermark()
        {
            this.Processor = new ImageProcessor.Processors.Watermark();
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

                        TextLayer textLayer = new TextLayer
                        {
                            Text = this.ParseText(queryString),
                            Position = this.ParsePosition(queryString),
                            FontColor = this.ParseColor(queryString),
                            FontSize = this.ParseFontSize(queryString),
                            FontFamily = this.ParseFontFamily(queryString),
                            Style = this.ParseFontStyle(queryString),
                            DropShadow = this.ParseDropShadow(queryString)
                        };

                        textLayer.Opacity = this.ParseOpacity(queryString, textLayer.FontColor);

                        this.Processor.DynamicParameter = textLayer;
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }

        #region Private Methods
        /// <summary>
        /// Returns the correct <see cref="T:System.String"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.String"/> for the given string.
        /// </returns>
        private string ParseText(string input)
        {
            foreach (Match match in TextRegex.Matches(input))
            {
                // split on text-
                return match.Value.Split(new[] { '=', '-' })[1].Replace("+", " ");
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Point"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Point"/>
        /// </returns>
        private Point? ParsePosition(string input)
        {
            foreach (Match match in PositionRegex.Matches(input))
            {
                // Chop off the leading legacy support '='
                int[] position = match.Value.TrimStart('=').ToPositiveIntegerArray();

                if (position != null)
                {
                    int x = position[0];
                    int y = position[1];

                    return new Point(x, y);
                }
            }

            return null;
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
                string value = match.Value.Split(new[] { '=', '-' })[1];
                Color textColor = CommonParameterParserUtility.ParseColor(value);
                if (!textColor.Equals(Color.Transparent))
                {
                    return textColor;
                }
            }

            return Color.Black;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/>
        /// </returns>
        private int ParseFontSize(string input)
        {
            foreach (Match match in FontSizeRegex.Matches(input))
            {
                // split on size-value
                return int.Parse(match.Value.Split(new[] { '=', '-' })[1], CultureInfo.InvariantCulture);
            }

            // Matches the default number in TextLayer.
            return 48;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.FontStyle"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The string containing the respective font style.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.FontStyle"/>
        /// </returns>
        private FontStyle ParseFontStyle(string input)
        {
            FontStyle fontStyle = FontStyle.Bold;

            foreach (Match match in FontStyleRegex.Matches(input))
            {
                // split on style-
                switch (match.Value.Split(new[]
                                              {
                                                  '=', '-'
                                              })[1])
                {
                    case "italic":
                        fontStyle = FontStyle.Italic;
                        break;
                    case "regular":
                        fontStyle = FontStyle.Regular;
                        break;
                    case "strikeout":
                        fontStyle = FontStyle.Strikeout;
                        break;
                    case "underline":
                        fontStyle = FontStyle.Underline;
                        break;
                }
            }

            return fontStyle;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.String"/> containing the font family for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.String"/> containing the font family for the given string.
        /// </returns>
        private FontFamily ParseFontFamily(string input)
        {
            foreach (Match match in FontFamilyRegex.Matches(input))
            {
                // split on font-
                string font = match.Value.Split(new[] { '=', '-' })[1].Replace("+", " ");

                return new FontFamily(font);
            }

            return new FontFamily(GenericFontFamilies.SansSerif);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the opacity for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <param name="color">
        /// The <see cref="T:System.Drawing.Color"/> of the current <see cref="TextLayer"/>.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the opacity for the given string.
        /// </returns>
        private int ParseOpacity(string input, Color color)
        {
            if (color.A < 255)
            {
                return (color.A / 255) * 100;
            }

            foreach (Match match in OpacityRegex.Matches(input))
            {
                // Split on opacity-
                return int.Parse(match.Value.Split(new[] { '=', '-' })[1], CultureInfo.InvariantCulture);
            }

            // Full opacity - matches the TextLayer default.
            return 100;
        }

        /// <summary>
        /// Returns a value indicating whether the watermark is to have a shadow.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The true if the watermark is to have a shadow; otherwise false.
        /// </returns>
        private bool ParseDropShadow(string input)
        {
            return ShadowRegex.Matches(input).Cast<Match>().Any();
        }

        #endregion
    }
}
