// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Preset.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to that allow the processing of preset image processing instructions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Encapsulates methods to that allow the processing of preset image processing instructions.
    /// </summary>
    public class Preset : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"preset=[^&]*", RegexOptions.Compiled);

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
        /// Gets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            private set;
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
        /// The match regex index.
        /// </summary>
        /// <param name="queryString">
        /// The query string.
        /// </param>
        /// <returns>
        /// The <see cref="int"/>.
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
                        string preset = match.Value;
                        this.DynamicParameter = preset;
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
            string preset = this.DynamicParameter;
            string querystring;
            this.Settings.TryGetValue(preset.Split('=')[1], out querystring);

            string oldQueryString = factory.QueryString;
            string newQueryString = Regex.Replace(oldQueryString, preset, querystring ?? string.Empty);

            return factory.AddQueryString(newQueryString).AutoProcess().Image;
        }
    }
}
