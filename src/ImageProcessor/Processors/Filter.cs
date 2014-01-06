// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Filter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add filters to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging.Filters;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add filters to an image.
    /// </summary>
    public class Filter : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"filter=(lomograph|polaroid|blackwhite|sepia|greyscale|gotham|invert|hisatch|losatch|comic)", RegexOptions.Compiled);

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
            Bitmap newImage = null;
            Image image = factory.Image;
            IMatrixFilter matrix = null;

            try
            {
                // Don't use an object initializer here.
                // ReSharper disable once UseObjectOrCollectionInitializer
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                newImage.Tag = image.Tag;

                switch ((string)this.DynamicParameter)
                {
                    case "polaroid":
                        matrix = new PolaroidMatrixFilter();
                        break;
                    case "lomograph":
                        matrix = new LomographMatrixFilter();
                        break;
                    case "sepia":
                        matrix = new SepiaMatrixFilter();
                        break;
                    case "blackwhite":
                        matrix = new BlackWhiteMatrixFilter();
                        break;
                    case "greyscale":
                        matrix = new GreyScaleMatrixFilter();
                        break;
                    case "gotham":
                        matrix = new GothamMatrixFilter();
                        break;
                    case "invert":
                        matrix = new InvertMatrixFilter();
                        break;
                    case "hisatch":
                        matrix = new HiSatchMatrixFilter();
                        break;
                    case "losatch":
                        matrix = new LoSatchMatrixFilter();
                        break;
                    case "comic":
                        matrix = new ComicMatrixFilter();
                        break;
                }

                if (matrix != null)
                {
                    return matrix.TransformImage(factory, image, newImage);
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
    }
}
