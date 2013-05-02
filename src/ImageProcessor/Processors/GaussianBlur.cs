// -----------------------------------------------------------------------
// <copyright file="GaussianBlur.cs" company="James South">
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
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text.RegularExpressions;
    using ImageProcessor.Helpers.Extensions;
    #endregion

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// Adapted from <see cref="http://code.google.com/p/imagelibrary/source/browse/trunk/Filters/GaussianBlurFilter.cs"/>
    /// </summary>
    public class GaussianBlur : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"blur=(\d+(.\d+)?)", RegexOptions.Compiled);

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

            try
            {
                double radius = double.Parse(this.DynamicParameter);
                byte[] sourceBytes = image.ToBytes(factory.ImageFormat);
                byte[] destinationBytes = new byte[sourceBytes.Length];

                this.ApplyGaussianBlur(image.Width, image.Height, radius, 1, ref sourceBytes, ref destinationBytes);


                // Don't use an object initializer here.
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                newImage = (Bitmap)newImage.FromBytes(destinationBytes);
                newImage.Tag = image.Tag;


                image.Dispose();
                image = newImage;
            }
            catch (Exception ex)
            {
                var x = ex;
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
        /// The apply gaussian blur.
        /// </summary>
        /// <param name="width">
        /// The width.
        /// </param>
        /// <param name="height">
        /// The height.
        /// </param>
        /// <param name="radius">
        /// The radius.
        /// </param>
        /// <param name="sourceBytes">
        /// The source bytes array.
        /// </param>
        /// <param name="destinationBytes">
        /// The destination byte array.
        /// </param>
        private void ApplyGaussianBlur(int width, int height, double radius, double amount, ref byte[] src, ref byte[] dst)
        {

            int shift, dest, source;
            int blurDiam = (int)Math.Pow(radius, 2);
            int gaussWidth = (blurDiam * 2) + 1;

            double[] kernel = CreateKernel(gaussWidth, blurDiam);

            // Calculate the sum of the Gaussian kernel      
            double gaussSum = 0;
            for (int n = 0; n < gaussWidth; n++)
            {
                gaussSum += kernel[n];
            }

            // Scale the Gaussian kernel
            for (int n = 0; n < gaussWidth; n++)
            {
                kernel[n] = kernel[n] / gaussSum;
            }
            //premul = kernel[k] / gaussSum;


            // Create an X & Y pass buffer  
            byte[] gaussPassX = new byte[src.Length];

            // Do Horizontal Pass  
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dest = y * width + x;

                    // Iterate through kernel  
                    for (int k = 0; k < gaussWidth; k++)
                    {

                        // Get pixel-shift (pixel dist between dest and source)  
                        shift = k - blurDiam;

                        // Basic edge clamp  
                        source = dest + shift;
                        if (x + shift <= 0 || x + shift >= width)
                        {
                            source = dest;
                        }

                        // Combine source and destination pixels with Gaussian Weight  
                        gaussPassX[(dest << 2) + 2] = (byte)(gaussPassX[(dest << 2) + 2] + (src[(source << 2) + 2]) * kernel[k]);
                        gaussPassX[(dest << 2) + 1] = (byte)(gaussPassX[(dest << 2) + 1] + (src[(source << 2) + 1]) * kernel[k]);
                        gaussPassX[(dest << 2)] = (byte)(gaussPassX[(dest << 2)] + (src[(source << 2)]) * kernel[k]);
                    }
                }
            }

            // Do Vertical Pass  
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    dest = y * width + x;

                    // Iterate through kernel  
                    for (int k = 0; k < gaussWidth; k++)
                    {
                        // Get pixel-shift (pixel dist between dest and source)   
                        shift = k - blurDiam;

                        // Basic edge clamp  
                        source = dest + (shift * width);
                        if (y + shift <= 0 || y + shift >= height)
                        {
                            source = dest;
                        }

                        // Combine source and destination pixels with Gaussian Weight  
                        dst[(dest << 2) + 2] = (byte)(dst[(dest << 2) + 2] + (gaussPassX[(source << 2) + 2]) * kernel[k]);
                        dst[(dest << 2) + 1] = (byte)(dst[(dest << 2) + 1] + (gaussPassX[(source << 2) + 1]) * kernel[k]);
                        dst[(dest << 2)] = (byte)(dst[(dest << 2)] + (gaussPassX[(source << 2)]) * kernel[k]);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the Gaussian kernel.
        /// </summary>
        /// <param name="gaussianWidth">
        /// The gaussian width.
        /// </param>
        /// <param name="blurDiameter">
        /// The blur diameter.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        private double[] CreateKernel(int gaussianWidth, int blurDiam)
        {

            double[] kernel = new double[gaussianWidth];

            // Set the maximum value of the Gaussian curve  
            const double sd = 255;

            // Set the width of the Gaussian curve  
            double range = gaussianWidth;

            // Set the average value of the Gaussian curve   
            double mean = (range / sd);

            // Set first half of Gaussian curve in kernel  
            for (int pos = 0, len = blurDiam + 1; pos < len; pos++)
            {
                // Distribute Gaussian curve across kernel[array]   
                kernel[gaussianWidth - 1 - pos] = kernel[pos] = Math.Sqrt(Math.Sin((((pos + 1) * (Math.PI / 2)) - mean) / range)) * sd;
            }

            return kernel;
        }
        #endregion
    }
}
