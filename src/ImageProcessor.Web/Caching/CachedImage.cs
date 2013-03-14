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
    internal sealed class CachedImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedImage"/> class.
        /// </summary>
        /// <param name="path">
        /// The value of the cached image.
        /// </param>
        /// <param name="naxAge">
        /// The maximum age in days cached image.
        /// </param>
        /// <param name="lastWriteTimeUtc">
        /// The last write time of the cached image.
        /// </param>
        /// <param name="expiresTimeUtc">
        /// The expires time.
        /// </param>
        public CachedImage(string path, int maxAge, DateTime lastWriteTimeUtc, DateTime expiresTimeUtc)
        {
            this.Path = path;
            this.MaxAge = maxAge;
            this.LastWriteTimeUtc = lastWriteTimeUtc;
            this.ExpiresUtc = expiresTimeUtc;
        }

        /// <summary>
        /// Gets or sets the value of the cached image.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The maximum age of the cached image in days.
        /// </summary>
        public int MaxAge { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the cached image.
        /// </summary>
        public DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets when the cached image should expire from the cache.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }
    }
}
