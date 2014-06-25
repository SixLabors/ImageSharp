// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Flip.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Flips an image horizontally or vertically.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Flips an image horizontally or vertically.
    /// </summary>
    public class Flip : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// <see href="http://stackoverflow.com/a/6400969/427899"/>
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"flip=(horizontal|vertical|both)", RegexOptions.Compiled);

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
                        string direction = match.Value.Split('=')[1];

                        switch (direction)
                        {
                            case "horizontal":
                                this.DynamicParameter = RotateFlipType.RotateNoneFlipX;
                                break;
                            case "vertical":
                                this.DynamicParameter = RotateFlipType.RotateNoneFlipY;
                                break;
                            default:
                                this.DynamicParameter = RotateFlipType.RotateNoneFlipXY;
                                break;
                        }
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
                RotateFlipType rotateFlipType = this.DynamicParameter;

                newImage = (Bitmap)image.Clone();

                // Flip
                newImage.RotateFlip(rotateFlipType);

                image.Dispose();
                image = newImage;
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
    }
}
