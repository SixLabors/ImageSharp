// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Mask.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Applies a mask to the given image. If the mask is not the same size as the image
//   it will be centered against the image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web.Hosting;

    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Applies a mask to the given image. If the mask is not the same size as the image 
    /// it will be centered against the image.
    /// </summary>
    public class Mask : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(mask=|maskposition=)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The mask image regex.
        /// </summary>
        private static readonly Regex PixelRegex = new Regex(@"mask=[\w+-]+." + ImageHelpers.ExtensionRegexPattern);

        /// <summary>
        /// The point regex.
        /// </summary>
        private static readonly Regex PointRegex = new Regex(@"maskposition=\d+,\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Mask"/> class.
        /// </summary>
        public Mask()
        {
            this.Processor = new ImageProcessor.Processors.Mask();
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
        /// <param name="queryString">The query string to search.</param>
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
                Image image = this.ParseImage(toParse);
                Point? rectangle = this.ParsePoint(toParse);
                this.Processor.DynamicParameter = new Tuple<Image, Point?>(image, rectangle);
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Returns the correct size of pixels.
        /// </summary>
        /// <param name="input">
        /// The input containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="int"/> representing the pixel size.
        /// </returns>
        public Image ParseImage(string input)
        {
            Image image = null;

            // Correctly parse the path.
            string path;
            this.Processor.Settings.TryGetValue("VirtualPath", out path);

            if (!string.IsNullOrWhiteSpace(path) && path.StartsWith("~/"))
            {
                Match match = PixelRegex.Match(input);

                if (match.Success)
                {
                    string imagePath = HostingEnvironment.MapPath(path);
                    if (imagePath != null)
                    {
                        imagePath = Path.Combine(imagePath, match.Value.Split('=')[1]);
                        using (ImageFactory factory = new ImageFactory())
                        {
                            factory.Load(imagePath);
                            image = new Bitmap(factory.Image);
                        }
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// Returns the correct <see cref="Nullable{Point}"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="Nullable{Point}"/>
        /// </returns>
        private Point? ParsePoint(string input)
        {
            int[] dimensions = { };

            foreach (Match match in PointRegex.Matches(input))
            {
                dimensions = match.Value.ToPositiveIntegerArray();
            }

            if (dimensions.Length == 2)
            {
                return new Point(dimensions[0], dimensions[1]);
            }

            return null;
        }
    }
}
