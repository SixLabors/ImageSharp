// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IImageCache.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Defines properties and methods for allowing caching of images to different sources.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>
    ///  Defines properties and methods for allowing caching of images to different sources.
    /// </summary>
    public interface IImageCache
    {
        /// <summary>
        /// Gets or sets any additional settings required by the cache.
        /// </summary>
        Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets the path to the cached image.
        /// </summary>
        string CachedPath { get; }

        /// <summary>
        /// Gets or sets the maximum number of days to store the image.
        /// </summary>
        int MaxDays { get; set; }

        /// <summary>
        /// Gets a value indicating whether the image is new or updated in an asynchronous manner.
        /// </summary>
        /// <returns>
        /// The asynchronous <see cref="Task"/> returning the value.
        /// </returns>
        Task<bool> IsNewOrUpdatedAsync();

        /// <summary>
        /// Adds the image to the cache in an asynchronous manner.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the image data.
        /// </param>
        /// <param name="contentType">
        /// The content type of the image.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> representing an asynchronous operation.
        /// </returns>
        Task AddImageToCacheAsync(Stream stream, string contentType);

        /// <summary>
        /// Trims the cache of any expired items in an asynchronous manner.
        /// </summary>
        /// <returns>
        /// The asynchronous <see cref="Task"/> representing an asynchronous operation.
        /// </returns>
        Task TrimCacheAsync();

        /// <summary>
        /// Gets a string identifying the cached file name in an asynchronous manner.
        /// </summary>
        /// <returns>
        /// The asynchronous <see cref="Task"/> returning the value.
        /// </returns>
        Task<string> CreateCachedFileNameAsync();

        /// <summary>
        /// Rewrites the path to point to the cached image.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext"/> encapsulating all information about the request.
        /// </param>
        void RewritePath(HttpContext context);
    }
}
