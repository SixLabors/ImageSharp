// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CachedImage.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Describes a cached image
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    using System;

    /// <summary>
    /// Describes a cached image 
    /// </summary>
    public sealed class CachedImage
    {
        /// <summary>
        /// Gets or sets the key identifying the cached image.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the cached image.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the cached image.
        /// </summary>
        public DateTime CreationTimeUtc { get; set; }
    }
}
