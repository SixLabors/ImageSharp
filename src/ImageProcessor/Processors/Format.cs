// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Format.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Sets the output of the image to a specific format.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Sets the output of the image to a specific format.
    /// </summary>
    public class Format : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"format=(j(pg|peg)|pn(g8|g)|bmp|gif|ti(ff|f)|ico)", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
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
                        this.DynamicParameter = match.Value.Split('=')[1];
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
            string format = this.DynamicParameter;
            bool isIndexed = false;
            ImageFormat imageFormat;
            switch (format)
            {
                case "png":
                    imageFormat = ImageFormat.Png;
                    break;
                case "png8":
                    imageFormat = ImageFormat.Png;
                    isIndexed = true;
                    break;
                case "bmp":
                    imageFormat = ImageFormat.Bmp;
                    break;
                case "gif":
                    imageFormat = ImageFormat.Gif;
                    isIndexed = true;
                    break;
                case "tif":
                case "tiff":
                    imageFormat = ImageFormat.Tiff;
                    break;
                case "ico":
                    imageFormat = ImageFormat.Icon;
                    break;
                default:
                    // Should be a jpeg or jpg.
                    imageFormat = ImageFormat.Jpeg;
                    break;
            }

            // Set the internal property.
            factory.OriginalExtension = string.Format(".{0}", format);
            // TODO: Fix this.
            //factory.Format(imageFormat);

            return factory.Image;
        }
        #endregion
    }
}
