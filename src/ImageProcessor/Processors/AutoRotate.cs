// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AutoRotate.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs auto-rotation to ensure that EXIF defined rotation is reflected in
//   the final image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Performs auto-rotation to ensure that EXIF defined rotation is reflected in 
    /// the final image.
    /// </summary>
    public class AutoRotate : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"autorotate=true", RegexOptions.Compiled);

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
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">The the current instance of the 
        /// <see cref="T:ImageProcessor.ImageFactory" /> class containing
        /// the image to process.</param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory" /> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                const int Orientation = (int)ExifPropertyTag.Orientation;
                if (!factory.PreserveExifData && factory.ExifPropertyItems.ContainsKey(Orientation))
                {
                    newImage = new Bitmap(image);

                    int rotationValue = factory.ExifPropertyItems[Orientation].Value[0];
                    switch (rotationValue)
                    {
                        case 1: // Landscape, do nothing
                            break;

                        case 8: // Rotated 90 right
                            // De-rotate:
                            newImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
                            break;

                        case 3: // Bottoms up
                            newImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            break;

                        case 6: // Rotated 90 left
                            newImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            break;
                    }

                    // Reassign the image.
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
    }
}