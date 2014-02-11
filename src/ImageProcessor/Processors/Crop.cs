// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Crop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Crops an image to the given directions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Extensions;
    using ImageProcessor.Imaging;

    #endregion

    /// <summary>
    /// Crops an image to the given directions.
    /// </summary>
    public class Crop : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// <see cref="http://stackoverflow.com/a/6400969/427899"/>
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"crop=\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?|cropmode=(pixels|percent)", RegexOptions.Compiled);

        /// <summary>
        /// The coordinate regex.
        /// </summary>
        private static readonly Regex CoordinateRegex = new Regex(@"crop=\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// The mode regex.
        /// </summary>
        private static readonly Regex ModeRegex = new Regex(@"cropmode=(pixels|percent)", RegexOptions.Compiled);

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

            // First merge the matches so we can parse .
            StringBuilder stringBuilder = new StringBuilder();

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;
                    }

                    stringBuilder.Append(match.Value);

                    index += 1;
                }
            }

            if (this.SortOrder < int.MaxValue)
            {
                // Match syntax
                string toParse = stringBuilder.ToString();

                float[] coordinates = this.ParseCoordinates(toParse);
                CropMode cropMode = this.ParseMode(toParse);

                CropLayer cropLayer = new CropLayer(coordinates[0], coordinates[1], coordinates[2], coordinates[3], cropMode);
                this.DynamicParameter = cropLayer;
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
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                RectangleF rectangleF;
                CropLayer cropLayer = this.DynamicParameter;

                if (cropLayer.CropMode == CropMode.Percentage)
                {
                    // Work out the percentages.
                    float left = cropLayer.Left * sourceWidth;
                    float top = cropLayer.Top * sourceWidth;
                    float right = sourceWidth - (cropLayer.Right * sourceWidth);
                    float bottom = sourceHeight - (cropLayer.Bottom * sourceHeight);
                    //float right = (sourceWidth - (cropLayer.Right * sourceWidth)) - left;
                    //float bottom = (sourceHeight - (cropLayer.Bottom * sourceHeight)) - top;

                    rectangleF = new RectangleF(left, top, right, bottom);
                }
                else
                {
                    rectangleF = new RectangleF(cropLayer.Left, cropLayer.Top, cropLayer.Right, cropLayer.Bottom);
                }

                Rectangle rectangle = Rectangle.Round(rectangleF);

                if (rectangle.X < sourceWidth && rectangle.Y < sourceHeight)
                {
                    if (rectangle.Width > (sourceWidth - rectangle.X))
                    {
                        rectangle.Width = sourceWidth - rectangle.X;
                    }

                    if (rectangle.Height > (sourceHeight - rectangle.Y))
                    {
                        rectangle.Height = sourceHeight - rectangle.Y;
                    }

                    newImage = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;

                        // An unwanted border appears when using InterpolationMode.HighQualityBicubic to resize the image
                        // as the algorithm appears to be pulling averaging detail from surrounding pixels beyond the edge 
                        // of the image. Using the ImageAttributes class to specify that the pixels beyond are simply mirror 
                        // images of the pixels within solves this problem.
                        using (ImageAttributes wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            graphics.DrawImage(
                                image,
                                new Rectangle(0, 0, rectangle.Width, rectangle.Height),
                                rectangle.X,
                                rectangle.Y,
                                rectangle.Width,
                                rectangle.Height,
                                GraphicsUnit.Pixel,
                                wrapMode);
                        }

                        // Reassign the image.
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

        /// <summary>
        /// Returns the correct <see cref="CropMode"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="CropMode"/>.
        /// </returns>
        private CropMode ParseMode(string input)
        {
            foreach (Match match in ModeRegex.Matches(input))
            {
                // Split on =
                string mode = match.Value.Split('=')[1];

                switch (mode)
                {
                    case "percent":
                        return CropMode.Percentage;
                    case "pixels":
                        return CropMode.Pixels;
                }
            }

            return CropMode.Pixels;
        }


        /// <summary>
        /// Returns the correct <see cref="CropMode"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="CropMode"/>.
        /// </returns>
        private float[] ParseCoordinates(string input)
        {
            float[] floats = { };

            foreach (Match match in CoordinateRegex.Matches(input))
            {
                floats = match.Value.ToPositiveFloatArray();
            }

            return floats;
        }
    }
}
