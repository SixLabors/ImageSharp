// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MatrixFilterRegexAttribute.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The filter attribute for identifying matrix filter properties.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters
{
    using System;

    /// <summary>
    /// The filter attribute for identifying matrix filter properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class MatrixFilterRegexAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixFilterRegexAttribute"/> class.
        /// </summary>
        /// <param name="regexIdentifier">
        /// The regex identifier.
        /// </param>
        public MatrixFilterRegexAttribute(string regexIdentifier)
        {
            this.RegexIdentifier = regexIdentifier;
        }

        /// <summary>
        /// Gets the regex identifier.
        /// </summary>
        public string RegexIdentifier { get; private set; }
    }
}
