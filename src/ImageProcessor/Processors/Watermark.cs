// -----------------------------------------------------------------------
// <copyright file="Watermark.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Text;
    using System.Linq;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates methods to change the alpha component of the image to effect its transparency.
    /// </summary>
    public class Watermark : IGraphicsProcessor
    {
/// <summary>
/// The regular expression to search strings for.
/// </summary>
private static readonly Regex QueryRegex = new Regex(@"watermark=[^&]*", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the text attribute.
/// </summary>
private static readonly Regex TextRegex = new Regex(@"text-[^/:?#\[\]@!$&'()*%\|,;=]+", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the position attribute.
/// </summary>
private static readonly Regex PositionRegex = new Regex(@"position-\d+-\d+", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the color attribute.
/// </summary>
private static readonly Regex ColorRegex = new Regex(@"color-([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the fontsize attribute.
/// </summary>
private static readonly Regex FontSizeRegex = new Regex(@"size-\d{1,3}", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the fontstyle attribute.
/// </summary>
private static readonly Regex FontStyleRegex = new Regex(@"style-(bold|italic|regular|strikeout|underline)", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the font family attribute.
/// </summary>
private static readonly Regex FontFamilyRegex = new Regex(@"font-[^/:?#\[\]@!$&'()*%\|,;=0-9]+", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the opacity attribute.
/// </summary>
private static readonly Regex OpacityRegex = new Regex(@"opacity-(?:100|[1-9]?[0-9])", RegexOptions.Compiled);

/// <summary>
/// The regular expression to search strings for the shadow attribute.
/// </summary>
private static readonly Regex ShadowRegex = new Regex(@"shadow-true", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                return "Watermark";
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                return "Adds a watermark containing text to the image.";
            }
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
        /// Gets or sets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            set;
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
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
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

                        TextLayer textLayer = new TextLayer();

                        string toParse = match.Value;

                        textLayer.Text = this.ParseText(toParse);
                        textLayer.Position = this.ParsePosition(toParse);
                        textLayer.TextColor = this.ParseColor(toParse);
                        textLayer.FontSize = this.ParseFontSize(toParse);
                        textLayer.Font = this.ParseFontFamily(toParse);
                        textLayer.Style = this.ParseFontStyle(toParse);
                        textLayer.Opacity = this.ParseOpacity(toParse);
                        textLayer.DropShadow = this.ParseDropShadow(toParse);

                        this.DynamicParameter = textLayer;
                    }

                    index += 1;
                }
            }

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
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                // Dont use an object initializer here.
                newImage = new Bitmap(image);
                newImage.Tag = image.Tag;

                TextLayer textLayer = this.DynamicParameter;
                string text = textLayer.Text;
                int opacity = textLayer.Opacity;
                int fontSize = textLayer.FontSize;
                FontStyle fontStyle = textLayer.Style;

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (Font font = this.GetFont(textLayer.Font, fontSize, fontStyle))
                    {
                        using (StringFormat drawFormat = new StringFormat())
                        {
                            using (Brush brush = new SolidBrush(Color.FromArgb(opacity, textLayer.TextColor)))
                            {
                                Point origin = textLayer.Position;

                                // We need to ensure that there is a position set for the watermark
                                if (origin == Point.Empty)
                                {
                                    // Work out the size of the text.
                                    SizeF textSize = graphics.MeasureString(text, font, new SizeF(image.Width, image.Height), drawFormat);

                                    int x = (int)(image.Width - textSize.Width) / 2;
                                    int y = (int)(image.Height - textSize.Height) / 2;
                                    origin = new Point(x, y);
                                }

                                // Set the hinting and draw the text.
                                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                                if (textLayer.DropShadow)
                                {
                                    // Shadow opacity should change with the base opacity.
                                    int shadowOpacity = opacity - (int)Math.Ceiling((30 / 100f) * 255);
                                    int finalShadowOpacity = shadowOpacity > 0 ? shadowOpacity : 0;

                                    using (Brush shadowBrush = new SolidBrush(Color.FromArgb(finalShadowOpacity, Color.Black)))
                                    {
                                        // Scale the shadow position to match the font size.
                                        // Magic number but it's based on artistic preference.
                                        int shadowDiff = (int)Math.Ceiling(fontSize / 24f);
                                        Point shadowPoint = new Point(origin.X + shadowDiff, origin.Y + shadowDiff);
                                        graphics.DrawString(text, font, shadowBrush, shadowPoint, drawFormat);
                                    }
                                }

                                graphics.DrawString(text, font, brush, origin, drawFormat);
                            }
                        }
                    }

                    image.Dispose();
                    image = newImage;
                }
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return image;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Font"/> for the given parameters
        /// </summary>
        /// <param name="font">
        /// The font.
        /// </param>
        /// <param name="fontSize">
        /// The font size.
        /// </param>
        /// <param name="fontStyle">
        /// The font style.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Font"/>
        /// </returns>
        private Font GetFont(string font, int fontSize, FontStyle fontStyle)
        {
            try
            {
                using (FontFamily fontFamily = new FontFamily(font))
                {
                    return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
            catch
            {
                using (FontFamily fontFamily = FontFamily.GenericSansSerif)
                {
                    return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
        }

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
                return match.Value.Split('-')[1].Replace("+", " ");
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
        private Point ParsePosition(string input)
        {
            foreach (Match match in PositionRegex.Matches(input))
            {
                int[] position = match.Value.ToIntegerArray();

                if (position != null)
                {
                    int x = position[0];
                    int y = position[1];

                    return new Point(x, y);
                }
            }

            return Point.Empty;
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
                // split on color-hex
                return ColorTranslator.FromHtml("#" + match.Value.Split('-')[1]);
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
                return int.Parse(match.Value.Split('-')[1]);
            }

            // Matches the default number in TextLayer.
            return 48;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.FontStyle"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The string containing the respective fontstyle.
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
                switch (match.Value.Split('-')[1])
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
        private string ParseFontFamily(string input)
        {
            foreach (Match match in FontFamilyRegex.Matches(input))
            {
                // split on font-
                string font = match.Value.Split('-')[1].Replace("+", " ");

                return font;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the opacity for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the opacity for the given string.
        /// </returns>
        private int ParseOpacity(string input)
        {
            foreach (Match match in OpacityRegex.Matches(input))
            {
                // split on opacity-
                return int.Parse(match.Value.Split('-')[1]);
            }

            // full opacity - matches the Textlayer default.
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
