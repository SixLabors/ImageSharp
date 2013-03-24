// -----------------------------------------------------------------------
// <copyright file="IGraphicsProcessor.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using

    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Defines properties and methods for ImageProcessor Plugins.
    /// </summary>
    public interface IGraphicsProcessor
    {
        #region MetaData
        /// <summary>
        /// Gets the name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        string Description { get; }
        #endregion

        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        Regex RegexPattern { get; }

        /// <summary>
        /// Gets DynamicParameter.
        /// </summary>
        dynamic DynamicParameter { get; }

        /// <summary>
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        Dictionary<string, string> Settings { get; set; }

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
        Image ProcessImage(ImageFactory factory);
        #endregion
    }
}
