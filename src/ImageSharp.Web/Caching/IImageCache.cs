// <copyright file="IImageCache.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;

    /// <summary>
    /// Specifies the contract for caching images.
    /// TODO: Do we add cleanup to this? Scalable caches probably shouldn't do so.
    /// </summary>
    public interface IImageCache
    {
        /// <summary>
        /// Gets or sets any additional settings.
        /// </summary>
        IDictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="environment">The hosting environment the application is running in.</param>
        /// <param name="key">The cache key.</param>
        /// <returns>The <see cref="Task{Byte}"/></returns>
        Task<byte[]> GetAsync(IHostingEnvironment environment, string key);

        /// <summary>
        /// Returns a value indicating whether the current cached item is expired.
        /// </summary>
        /// <param name="environment">The hosting environment the application is running in.</param>
        /// <param name="key">The cache key.</param>
        /// <param name="maxDateUtc">The maximum allowable Utc date to store the items</param>
        /// <returns>The <see cref="Task{Boolean}"/></returns>
        Task<bool> IsExpiredAsync(IHostingEnvironment environment, string key, DateTime maxDateUtc);

        /// <summary>
        /// Sets the value associated with the specified key.
        /// </summary>
        /// <param name="environment">The hosting environment the application is running in.</param>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to store</param>
        /// <param name="creationDateUtc">The Utc creation date to apply to the cached item.</param>
        /// <returns>The <see cref="Task"/></returns>
        Task SetAsync(IHostingEnvironment environment, string key, byte[] value, DateTime creationDateUtc);
    }
}