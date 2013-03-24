// -----------------------------------------------------------------------
// <copyright file="CachedImage.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
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
        /// <param name="maxAge">
        /// The max age of the cached image.
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
        internal string Path { get; set; }

        /// <summary>
        /// Gets or sets the maximum age of the cached image in days.
        /// </summary>
        internal int MaxAge { get; set; }

        /// <summary>
        /// Gets or sets the last write time of the cached image.
        /// </summary>
        internal DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets when the cached image should expire from the cache.
        /// </summary>
        internal DateTime ExpiresUtc { get; set; }
    }
}
