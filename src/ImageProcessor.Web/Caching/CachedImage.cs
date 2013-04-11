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

    using SQLite;

    #endregion

    /// <summary>
    /// Describes a cached image 
    /// </summary>
    public sealed class CachedImage
    {
        /// <summary>
        /// Gets or sets the id identifying the cached image.
        /// </summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the key identifying the cached image.
        /// </summary>
        [Unique]
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the cached image.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the maximum age of the cached image in days.
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
