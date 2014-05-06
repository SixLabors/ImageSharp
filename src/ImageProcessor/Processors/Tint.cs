// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Tint.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Tints an image with the given colour.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Text.RegularExpressions;
    using ImageProcessor.Extensions;

    /// <summary>
    /// Tints an image with the given colour.
    /// </summary>
    public class Tint : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"tint=(\d+,\d+,\d+,\d+|([0-9a-fA-F]{3}){1,2})", RegexOptions.Compiled);

        #region IGraphicsProcessor Members

        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public Regex RegexPattern
        {
            get { return QueryRegex; }
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
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
                        this.DynamicParameter = this.ParseColor(match.Value);
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
                Color tintColour = (Color)this.DynamicParameter;
                float[][] colorMatrixElements =
                    {
                        new[] { tintColour.R / 255f, 0, 0, 0, 0 }, // Red 
                        new[] { 0, tintColour.G / 255f, 0, 0, 0 }, // Green 
                        new[] { 0, 0, tintColour.B / 255f, 0, 0 }, // Blue  
                        new[] { 0, 0, 0, tintColour.A / 255f, 0 }, // Alpha 
                        new float[] { 0, 0, 0, 0, 1 }
                    };

                ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;

                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);

                        image.Dispose();
                        image = newImage;
                    }
                }

                return image;
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
            foreach (Match match in QueryRegex.Matches(input))
            {
                string value = match.Value.Split('=')[1];

                if (value.Contains(","))
                {
                    int[] split = value.ToPositiveIntegerArray();
                    byte red = split[0].ToByte();
                    byte green = split[1].ToByte();
                    byte blue = split[2].ToByte();
                    byte alpha = split[3].ToByte();

                    return Color.FromArgb(alpha, red, green, blue);
                }

                // Split on color-hex
                return ColorTranslator.FromHtml("#" + value);
            }

            return Color.Transparent;
        }
    }
}
