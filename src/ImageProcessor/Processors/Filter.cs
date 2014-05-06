// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Filter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add filters to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging.Filters;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add filters to an image.
    /// </summary>
    public class Filter : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = BuildRegex(); 

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
                newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);

                IMatrixFilter matrix = this.DynamicParameter as IMatrixFilter ?? this.ParseFilter((string)this.DynamicParameter);

                if (matrix != null)
                {
                    return matrix.TransformImage(factory, image, newImage);
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
        /// Builds a regular expression from the <see cref="MatrixFilters"/> type, this allows extensibility.
        /// </summary>
        /// <returns>
        /// The <see cref="Regex"/> to match matrix filters.
        /// </returns>
        private static Regex BuildRegex()
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
            Type type = typeof(MatrixFilters);
            IEnumerable<PropertyInfo> filters = type.GetProperties(Flags)
                              .Where(p => p.IsDefined(typeof(MatrixFilterRegexAttribute), false))
                              .ToList();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("filter=(");
            int counter = 0;

            foreach (PropertyInfo filter in filters)
            {
                MatrixFilterRegexAttribute attribute = (MatrixFilterRegexAttribute)filter.GetCustomAttributes(typeof(MatrixFilterRegexAttribute), false).First();

                if (counter == 0)
                {
                    stringBuilder.Append(attribute.RegexIdentifier);
                }
                else
                {
                    stringBuilder.AppendFormat("|{0}", attribute.RegexIdentifier);
                }

                counter++;
            }

            stringBuilder.Append(")");

            return new Regex(stringBuilder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Parses the filter.
        /// </summary>
        /// <param name="identifier">
        /// The identifier.
        /// </param>
        /// <returns>
        /// The <see cref="IMatrixFilter"/>.
        /// </returns>
        private IMatrixFilter ParseFilter(string identifier)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;

            Type type = typeof(MatrixFilters);
            PropertyInfo filter =
                type.GetProperties(Flags)
                    .Where(p => p.IsDefined(typeof(MatrixFilterRegexAttribute), false))
                    .First(p => ((MatrixFilterRegexAttribute)p.GetCustomAttributes(typeof(MatrixFilterRegexAttribute), false).First()).RegexIdentifier == identifier);

            return filter.GetValue(null, null) as IMatrixFilter;
        }
    }
}
