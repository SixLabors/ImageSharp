// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Saturation.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to change the saturation component of the image.
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
    /// Encapsulates methods to change the saturation component of the image.
    /// </summary>
    /// <remarks>
    /// <see cref="http://www.bobpowell.net/imagesaturation.htm"/> 
    /// </remarks>
    public class Saturation : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// <see cref="http://stackoverflow.com/a/6400969/427899"/> 
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"saturation=(-?(?:100)|-?([1-9]?[0-9]))", RegexOptions.Compiled);

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
                        int percentage = int.Parse(match.Value.Split('=')[1]);

                        this.DynamicParameter = percentage;
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
                float saturationFactor = (float)this.DynamicParameter / 100;

                // Stop at -1 to prevent inversion.
                saturationFactor++;

                // The matrix is set up to "shear" the colour space using the following set of values.  
                // Note that each colour component has an effective luminance which contributes to the
                // overall brightness of the pixel.
                float saturationComplement = 1.0f - saturationFactor;
                float saturationComplementR = 0.3086f * saturationComplement;
                float saturationComplementG = 0.6094f * saturationComplement;
                float saturationComplementB = 0.0820f * saturationComplement;

                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);

                ColorMatrix colorMatrix =
                    new ColorMatrix(
                        new float[][]
                            {
                                new float[]
                                    {
                                        saturationComplementR + saturationFactor, saturationComplementR,
                                        saturationComplementR, 0, 0
                                    },
                                new float[]
                                    {
                                        saturationComplementG, saturationComplementG + saturationFactor,
                                        saturationComplementG, 0, 0
                                    },
                                new float[]
                                    {
                                        saturationComplementB, saturationComplementB,
                                        saturationComplementB + saturationFactor, 0, 0
                                    },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0, 0, 0, 0, 1 }
                            });

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (ImageAttributes imageAttributes = new ImageAttributes())
                    {
                        imageAttributes.SetColorMatrix(colorMatrix);

                        graphics.DrawImage(
                            image,
                            new Rectangle(0, 0, image.Width, image.Height),
                            0,
                            0,
                            image.Width,
                            image.Height,
                            GraphicsUnit.Pixel,
                            imageAttributes);

                        image.Dispose();
                        image = newImage;
                    }
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
