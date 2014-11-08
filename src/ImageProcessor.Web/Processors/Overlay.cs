// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Overlay.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Adds an image overlay to the current image.
//   If the overlay is larger than the image it will be scaled to match the image.
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

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// Adds an image overlay to the current image. 
    /// If the overlay is larger than the image it will be scaled to match the image.
    /// </summary>
    public class Overlay : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(overlay=|overlay.\w+=)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The overlay image regex.
        /// </summary>
        private static readonly Regex ImageRegex = new Regex(@"overlay=[\w+-]+." + ImageHelpers.ExtensionRegexPattern);

        /// <summary>
        /// The point regex.
        /// </summary>
        private static readonly Regex PointRegex = new Regex(@"overlay.position=\d+,\d+", RegexOptions.Compiled);

        /// <summary>
        /// The size regex.
        /// </summary>
        private static readonly Regex SizeRegex = new Regex(@"overlay.size=\d+,\d+", RegexOptions.Compiled);

        /// <summary>
        /// The opacity regex.
        /// </summary>
        private static readonly Regex OpacityRegex = new Regex(@"overlay.opacity=\d+", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Overlay"/> class.
        /// </summary>
        public Overlay()
        {
            this.Processor = new ImageProcessor.Processors.Overlay();
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
                this.Processor.DynamicParameter = new ImageLayer
                {
                    Image = this.ParseImage(toParse),
                    Position = this.ParsePoint(toParse),
                    Opacity = this.ParseOpacity(toParse),
                    Size = this.ParseSize(toParse)
                };
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
                Match match = ImageRegex.Match(input);

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

            Match match = PointRegex.Match(input);
            if (match.Success)
            {
                dimensions = match.Value.ToPositiveIntegerArray();
            }

            if (dimensions.Length == 2)
            {
                return new Point(dimensions[0], dimensions[1]);
            }

            return null;
        }

        /// <summary>
        /// Returns the correct <see cref="int"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="int"/>
        /// </returns>
        private int ParseOpacity(string input)
        {
            int opacity = 100;
            Match match = OpacityRegex.Match(input);
            if (match.Success)
            {
                opacity = Math.Abs(CommonParameterParserUtility.ParseIn100Range(match.Value.Split('=')[1]));
            }

            return opacity;
        }

        /// <summary>
        /// Returns the correct <see cref="Size"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        private Size ParseSize(string input)
        {
            Size size = Size.Empty;
            Match match = SizeRegex.Match(input);
            if (match.Success)
            {
                int[] dimensions = match.Value.ToPositiveIntegerArray();
                size = new Size(dimensions[0], dimensions[1]);
            }

            return size;
        }
    }
}
