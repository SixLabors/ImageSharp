// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GaussianSharpen.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Applies a Gaussian sharpen to the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Applies a Gaussian sharpen to the image.
    /// </summary>
    public class GaussianSharpen : IGraphicsProcessor
    {
        #region Fields
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"sharpen=[^&]*", RegexOptions.Compiled);

        /// <summary>
        /// The sharpen regex.
        /// </summary>
        private static readonly Regex SharpenRegex = new Regex(@"sharpen=\d+", RegexOptions.Compiled);

        /// <summary>
        /// The sigma regex.
        /// </summary>
        private static readonly Regex SigmaRegex = new Regex(@"sigma-\d+(.?\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// The threshold regex.
        /// </summary>
        private static readonly Regex ThresholdRegex = new Regex(@"threshold-\d+", RegexOptions.Compiled);
        #endregion

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
        /// Gets or sets the DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter { get; set; }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder { get; private set; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

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

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;

                        // Normalise and set the variables.
                        int maxSize;
                        double maxSigma;
                        int maxThreshold;

                        int.TryParse(this.Settings["MaxSize"], out maxSize);
                        double.TryParse(this.Settings["MaxSigma"], out maxSigma);
                        int.TryParse(this.Settings["MaxThreshold"], out maxThreshold);

                        int size = this.ParseSharpen(match.Value);
                        double sigma = this.ParseSigma(match.Value);
                        int threshold = this.ParseThreshold(match.Value);

                        size = maxSize < size ? maxSize : size;
                        sigma = maxSigma < sigma ? maxSigma : sigma;
                        threshold = maxThreshold < threshold ? maxThreshold : threshold;

                        this.DynamicParameter = new GaussianLayer(size, sigma, threshold);
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">The the current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class containing
        /// the image to process.</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Bitmap image = (Bitmap)factory.Image;

            try
            {
                newImage = new Bitmap(image);
                GaussianLayer gaussianLayer = (GaussianLayer)this.DynamicParameter;

                Convolution convolution = new Convolution(gaussianLayer.Sigma) { Threshold = gaussianLayer.Threshold };
                double[,] kernel = convolution.CreateGuassianSharpenFilter(gaussianLayer.Size);
                newImage = convolution.ProcessKernel(newImage, kernel);

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

        #region Private
        /// <summary>
        /// Returns the correct <see cref="T:System.Double"/> containing the sigma value
        /// for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Double"/> for the given string.
        /// </returns>
        private double ParseSigma(string input)
        {
            foreach (Match match in SigmaRegex.Matches(input))
            {
                // split on text-
                return Convert.ToDouble(match.Value.Split('-')[1]);
            }

            return 1.4d;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the threshold value
        /// for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> for the given string.
        /// </returns>
        private int ParseThreshold(string input)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (Match match in ThresholdRegex.Matches(input))
            {
                return Convert.ToInt32(match.Value.Split('-')[1]);
            }

            return 0;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the sharpen value
        /// for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> for the given string.
        /// </returns>
        private int ParseSharpen(string input)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (Match match in SharpenRegex.Matches(input))
            {
                return Convert.ToInt32(match.Value.Split('=')[1]);
            }

            return 0;
        }
        #endregion
    }
}
