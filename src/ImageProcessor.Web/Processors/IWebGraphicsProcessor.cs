// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IWebGraphicsProcessor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Defines properties and methods for ImageProcessor.Web Plugins.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Processors
{
    using System.Text.RegularExpressions;
    using ImageProcessor.Processors;

    /// <summary>
    /// Defines properties and methods for ImageProcessor.Web Plugins.
    /// </summary>
    public interface IWebGraphicsProcessor
    {
        #region Properties
        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        Regex RegexPattern { get; }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Gets the associated graphics processor.
        /// </summary>
        IGraphicsProcessor Processor { get; }
        #endregion

        #region Methods
        /// <summary>
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        int MatchRegexIndex(string queryString);
        #endregion
    }
}
