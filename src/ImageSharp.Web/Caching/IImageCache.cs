// <copyright file="IImageCache.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

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
        /// <param name="key">The cache key</param>
        /// <returns>The <see cref="Task{CachedBuffer}"/></returns>
        Task<CachedBuffer> GetAsync(string key);

        /// <summary>
        /// Returns a value indicating whether the current cached item is expired.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="minDateUtc">
        /// The minimum allowable date and time in coordinated universal time (UTC) since the file was last modified.
        /// Calculated as the current datetime minus the maximum allowable cached days.
        /// </param>
        /// <returns>The <see cref="Task{ImageCacheInfo}"/></returns>
        Task<CachedInfo> IsExpiredAsync(string key, DateTime minDateUtc);

        /// <summary>
        /// Sets the value associated with the specified key.
        /// Returns the date and time, in coordinated universal time (UTC), that the value was last written to.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to store</param>
        /// <param name="length">The length, in bytes, of the data within the buffer.</param>
        /// <returns>The <see cref="Task{DateTimeOffset}"/></returns>
        Task<DateTimeOffset> SetAsync(string key, byte[] value, int length);
    }
}