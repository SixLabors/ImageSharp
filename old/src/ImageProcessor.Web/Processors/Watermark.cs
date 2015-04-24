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
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Drawing.Text;
    using System.Text.RegularExpressions;
    using System.Web;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Encapsulates methods to add a watermark text overlay to an image.
    /// </summary>
    public class Watermark : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"watermark=", RegexOptions.Compiled);

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
            this.SortOrder = int.MaxValue;
            Match match = this.RegexPattern.Match(queryString);

            if (match.Success)
            {
                this.SortOrder = match.Index;
                NameValueCollection queryCollection = HttpUtility.ParseQueryString(queryString);
                TextLayer textLayer = new TextLayer
                {
                    Text = this.ParseText(queryCollection),
                    Position = this.ParsePosition(queryCollection),
                    FontColor = this.ParseColor(queryCollection),
                    FontSize = this.ParseFontSize(queryCollection),
                    FontFamily = this.ParseFontFamily(queryCollection),
                    Style = this.ParseFontStyle(queryCollection),
                    DropShadow = this.ParseDropShadow(queryCollection),
                    Vertical = this.ParseVertical(queryCollection),
                    RightToLeft = this.ParseRightToLeft(queryCollection)
                };

                textLayer.Opacity = this.ParseOpacity(queryCollection, textLayer.FontColor);

                this.Processor.DynamicParameter = textLayer;
            }

            return this.SortOrder;
        }

        #region Private Methods
        /// <summary>
        /// Returns the correct <see cref="T:System.String"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.String"/>.
        /// </returns>
        private string ParseText(NameValueCollection queryCollection)
        {
            return QueryParamParser.Instance.ParseValue<string>(queryCollection["watermark"]);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Point"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Point"/>
        /// </returns>
        private Point? ParsePosition(NameValueCollection queryCollection)
        {
            return queryCollection["textposition"] != null
                  ? QueryParamParser.Instance.ParseValue<Point>(queryCollection["textposition"])
                  : (Point?)null;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        private Color ParseColor(NameValueCollection queryCollection)
        {
            return queryCollection["color"] != null
                  ? QueryParamParser.Instance.ParseValue<Color>(queryCollection["color"])
                  : Color.Black;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/>
        /// </returns>
        private int ParseFontSize(NameValueCollection queryCollection)
        {
            return queryCollection["fontsize"] != null
                  ? QueryParamParser.Instance.ParseValue<int>(queryCollection["fontsize"])
                  : 48;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.FontStyle"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.FontStyle"/>
        /// </returns>
        private FontStyle ParseFontStyle(NameValueCollection queryCollection)
        {
            return queryCollection["fontstyle"] != null
                  ? QueryParamParser.Instance.ParseValue<FontStyle>(queryCollection["fontstyle"])
                  : FontStyle.Bold;
        }

        /// <summary>
        /// Returns the correct <see cref="FontFamily"/> for the given parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// The correct <see cref="FontFamily"/>.
        /// </returns>
        private FontFamily ParseFontFamily(NameValueCollection queryCollection)
        {
            return queryCollection["fontfamily"] != null
                  ? QueryParamParser.Instance.ParseValue<FontFamily>(queryCollection["fontfamily"])
                  : new FontFamily(GenericFontFamilies.SansSerif);
        }

        /// <summary>
        /// Returns a value indicating whether the watermark is to have a shadow.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// True if the watermark is to have a shadow; otherwise false.
        /// </returns>
        private bool ParseDropShadow(NameValueCollection queryCollection)
        {
            return QueryParamParser.Instance.ParseValue<bool>(queryCollection["dropshadow"]);
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the opacity for the parameter collection.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <param name="color">
        /// The <see cref="T:System.Drawing.Color"/> of the current <see cref="TextLayer"/>.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/>.
        /// </returns>
        private int ParseOpacity(NameValueCollection queryCollection, Color color)
        {
            if (color.A < 255)
            {
                return (color.A / 255) * 100;
            }

            return queryCollection["fontopacity"] != null
                  ? QueryParamParser.Instance.ParseValue<int>(queryCollection["fontopacity"])
                  : 100;
        }

        /// <summary>
        /// Returns a value indicating whether the watermark is to be written right to left.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// True if the watermark is to be written right to left; otherwise false.
        /// </returns>
        private bool ParseRightToLeft(NameValueCollection queryCollection)
        {
            return QueryParamParser.Instance.ParseValue<bool>(queryCollection["rtl"]);
        }

        /// <summary>
        /// Returns a value indicating whether the watermark is to be written vertically.
        /// </summary>
        /// <param name="queryCollection">
        /// The <see cref="NameValueCollection"/> of query parameters.
        /// </param>
        /// <returns>
        /// True if the watermark is to be written vertically; otherwise false.
        /// </returns>
        private bool ParseVertical(NameValueCollection queryCollection)
        {
            return QueryParamParser.Instance.ParseValue<bool>(queryCollection["vertical"]);
        }
        #endregion
    }
}
