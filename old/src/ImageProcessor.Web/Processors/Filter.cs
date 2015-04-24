// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Filter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add filters to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging.Filters.Photo;
    using ImageProcessor.Processors;

    /// <summary>
    /// Encapsulates methods with which to add filters to an image.
    /// </summary>
    public class Filter : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = BuildRegex();

        /// <summary>
        /// The filter cache.
        /// </summary>
        private static readonly ConcurrentDictionary<string, IMatrixFilter> FilterCache
            = new ConcurrentDictionary<string, IMatrixFilter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="Filter"/> class.
        /// </summary>
        public Filter()
        {
            this.Processor = new ImageProcessor.Processors.Filter();
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
        /// Gets the processor.
        /// </summary>
        /// <value>
        /// The processor.
        /// </value>
        public IGraphicsProcessor Processor { get; private set; }

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
            this.SortOrder = int.MaxValue;
            Match match = this.RegexPattern.Match(queryString);

            if (match.Success)
            {
                this.SortOrder = match.Index;
                this.Processor.DynamicParameter = this.ParseFilter(match.Value.Split('=')[1]);
            }

            return this.SortOrder;
        }

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
                              .Where(p => p.PropertyType.IsAssignableFrom(typeof(IMatrixFilter)))
                              .ToList();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("filter=(");
            int counter = 0;

            foreach (PropertyInfo filter in filters)
            {
                if (counter == 0)
                {
                    stringBuilder.Append(filter.Name.ToLowerInvariant());
                }
                else
                {
                    stringBuilder.AppendFormat("|{0}", filter.Name.ToLowerInvariant());
                }

                counter++;
            }

            stringBuilder.Append(")");

            return new Regex(stringBuilder.ToString(), RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Parses the input string to return the correct <see cref="IMatrixFilter"/>.
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
            return FilterCache.GetOrAdd(
                identifier,
                f =>
                {
                    Type type = typeof(MatrixFilters);
                    PropertyInfo filter =
                        type.GetProperties(Flags)
                            .Where(p => p.PropertyType.IsAssignableFrom(typeof(IMatrixFilter)))
                            .First(p => p.Name.Equals(identifier, StringComparison.InvariantCultureIgnoreCase));

                    return filter.GetValue(null, null) as IMatrixFilter;
                });
        }
    }
}
