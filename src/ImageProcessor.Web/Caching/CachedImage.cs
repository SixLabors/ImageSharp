// -----------------------------------------------------------------------
// <copyright file="CachedImage.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    #endregion

    /// <summary>
    /// Describes a cached image 
    /// </summary>
    public class CachedImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedImage"/> class.
        /// </summary>
        /// <param name="value">
        /// The value of the cached item.
        /// </param>
        /// <param name="lastWriteTimeUtc">
        /// The last write time of the cached item.
        /// </param>
        public CachedImage(string value, DateTime lastWriteTimeUtc)
        {
            this.Value = value;
            this.LastWriteTimeUtc = lastWriteTimeUtc;
        }

        /// <summary>
        /// Gets the value date time delimiter.
        /// </summary>
        public static string ValueLastWriteTimeDelimiter
        {
            get
            {
                return "|*|";
            }
        }

        /// <summary>
        /// Gets or sets the value of the cached item
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the cached item
        /// </summary>
        public DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// The value and last write time as a string.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string ValueAndLastWriteTimeUtcToString()
        {
            return string.Format("{0}{1}{2}", this.Value, ValueLastWriteTimeDelimiter, this.LastWriteTimeUtc);
        }
    }
}
