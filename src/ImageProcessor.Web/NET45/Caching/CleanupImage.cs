// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CleanupImage.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Describes a cached image for cleanup.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    #endregion

    /// <summary>
    /// Describes a cached image for cleanup
    /// </summary>
    public sealed class CleanupImage
    {
        /// <summary>
        /// Gets or sets the value of the cached image.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets when the cached image should expire from the cache.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }
    }
}
