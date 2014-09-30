// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DetectEdges.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Produces an image with the detected edges highlighted.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web.Compilation;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Processors;

    /// <summary>
    /// Produces an image with the detected edges highlighted.
    /// </summary>
    public class DetectEdges : IWebGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = BuildRegex();

        /// <summary>
        /// The regular expression to search strings for the greyscale attribute.
        /// </summary>
        private static readonly Regex GreyscaleRegex = new Regex(@"greyscale=false", RegexOptions.Compiled);

        /// <summary>
        /// The edge detectors.
        /// </summary>
        private static Dictionary<string, object> detectors;

        /// <summary>
        /// Initializes a new instance of the <see cref="DetectEdges"/> class.
        /// </summary>
        public DetectEdges()
        {
            this.Processor = new ImageProcessor.Processors.DetectEdges();
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
                        stringBuilder.Append(queryString);
                    }

                    index += 1;
                }
            }

            if (this.SortOrder < int.MaxValue)
            {
                // Match syntax
                string toParse = stringBuilder.ToString();
                IEdgeFilter filter = this.ParseFilter(toParse);
                bool greyscale = !GreyscaleRegex.IsMatch(toParse);

                this.Processor.DynamicParameter = new Tuple<IEdgeFilter, bool>(filter, greyscale);
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Builds a regular expression from the <see cref="IEdgeFilter"/> type, this allows extensibility.
        /// </summary>
        /// <returns>
        /// The <see cref="Regex"/> to match matrix filters.
        /// </returns>
        private static Regex BuildRegex()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("detectedges=(");

            Type type = typeof(IEdgeFilter);

            // Build a list of native IEdgeFilter instances.
            detectors = BuildManager.GetReferencedAssemblies()
                                    .Cast<Assembly>()
                                    .SelectMany(s => s.GetLoadableTypes())
                                    .Where(t => type.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                                    .ToDictionary(t => t.Name.ToLowerInvariant().Replace("edgefilter", string.Empty), Activator.CreateInstance);

            stringBuilder.Append(string.Join("|", detectors.Keys.ToList()));

            stringBuilder.Append(")");

            return new Regex(stringBuilder.ToString(), RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Parses the input string to return the correct <see cref="IEdgeFilter"/>.
        /// </summary>
        /// <param name="identifier">
        /// The identifier.
        /// </param>
        /// <returns>
        /// The <see cref="IEdgeFilter"/>.
        /// </returns>
        private IEdgeFilter ParseFilter(string identifier)
        {
            return (IEdgeFilter)detectors[this.RegexPattern.Match(identifier).Value.Split('=')[1]];
        }
    }
}
