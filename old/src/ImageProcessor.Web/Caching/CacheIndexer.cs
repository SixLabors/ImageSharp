// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheIndexer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an in memory collection of keys and values whose operations are concurrent.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Caching;

    /// <summary>
    /// Represents an in memory collection of cached images whose operations are concurrent.
    /// </summary>
    public static class CacheIndexer
    {
        /// <summary>
        /// Gets the <see cref="CachedImage"/> associated with the specified key.
        /// </summary>
        /// <param name="cachedPath">
        /// The cached path of the value to get.
        /// </param>
        /// <returns>
        /// The <see cref="CachedImage"/> matching the given key if the <see cref="CacheIndexer"/> contains an element with 
        /// the specified key; otherwise, null.
        /// </returns>
        public static CachedImage Get(string cachedPath)
        {
            string key = Path.GetFileNameWithoutExtension(cachedPath);
            CachedImage cachedImage = (CachedImage)MemCache.GetItem(key);
            return cachedImage;
        }

        /// <summary>
        /// Removes the value associated with the specified key.
        /// </summary>
        /// <param name="cachedPath">
        /// The key of the item to remove.
        /// </param>
        /// <returns>
        /// true if the <see cref="CacheIndexer"/> removes an element with 
        /// the specified key; otherwise, false.
        /// </returns>
        public static bool Remove(string cachedPath)
        {
            string key = Path.GetFileNameWithoutExtension(cachedPath);
            return MemCache.RemoveItem(key);
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary or returns the value if it exists.
        /// </summary>
        /// <param name="cachedImage">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The value of the item to add or get.
        /// </returns>
        public static CachedImage Add(CachedImage cachedImage)
        {
            // Add the CachedImage with a sliding expiration of 10 minutes.
            CacheItemPolicy policy = new CacheItemPolicy { SlidingExpiration = new TimeSpan(0, 10, 0) };

            if (new Uri(cachedImage.Path).IsFile)
            {
                policy.ChangeMonitors.Add(new HostFileChangeMonitor(new List<string> { cachedImage.Path }));

                MemCache.AddItem(Path.GetFileNameWithoutExtension(cachedImage.Key), cachedImage, policy);
            }
            else
            {
                MemCache.AddItem(Path.GetFileNameWithoutExtension(cachedImage.Key), cachedImage, policy);
            }

            return cachedImage;
        }
    }
}