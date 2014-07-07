// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Crop.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Crops an image to the given directions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Text;
    using System.Text.RegularExpressions;

    using ImageProcessor.Imaging;
    using ImageProcessor.Processors;
    using ImageProcessor.Web.Extensions;

    /// <summary>
    /// Crops an image to the given directions.
    /// </summary>
    public class Crop : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// <see href="http://stackoverflow.com/a/6400969/427899"/>
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"(crop=|cropmode=)[^&]+", RegexOptions.Compiled);

        /// <summary>
        /// The coordinate regex.
        /// </summary>
        private static readonly Regex CoordinateRegex = new Regex(@"crop=\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?[,-]\d+(.\d+)?", RegexOptions.Compiled);

        /// <summary>
        /// The mode regex.
        /// </summary>
        private static readonly Regex ModeRegex = new Regex(@"cropmode=(pixels|percent)", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Crop"/> class.
        /// </summary>
        public Crop()
        {
            this.Processor = new ImageProcessor.Processors.Crop();
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

                float[] coordinates = this.ParseCoordinates(toParse);
                CropMode cropMode = this.ParseMode(toParse);

                CropLayer cropLayer = new CropLayer(coordinates[0], coordinates[1], coordinates[2], coordinates[3], cropMode);
                this.Processor.DynamicParameter = cropLayer;
            }

            return this.SortOrder;
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
