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
    #region Using
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using ImageProcessor.Web.Helpers;
    #endregion

    /// <summary>
    /// Represents an in memory collection of keys and values whose operations are concurrent.
    /// </summary>
    internal static class CacheIndexer
    {
        #region Public
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
        public static async Task<CachedImage> GetValueAsync(string cachedPath)
        {
            string key = Path.GetFileNameWithoutExtension(cachedPath);
            CachedImage cachedImage = (CachedImage)MemCache.GetItem(key);

            if (cachedImage == null)
            {
                cachedImage = await TaskHelpers.Run(() => GetCachedImage(cachedPath));

                if (cachedImage != null)
                {
                    Add(cachedImage);
                }
            }

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
            // Add the CachedImage.
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.ChangeMonitors.Add(new HostFileChangeMonitor(new List<string> { cachedImage.Path }));

            MemCache.AddItem(cachedImage.Key, cachedImage, policy);
            return cachedImage;
        }
        #endregion

        /// <summary>
        /// Creates a new cached image from the cache instance on disk.
        /// </summary>
        /// <param name="cachePath">
        /// The cache path.
        /// </param>
        /// <returns>
        /// The <see cref="CachedImage"/> from the cache instance on disk.
        /// </returns>
        private static CachedImage GetCachedImage(string cachePath)
        {
            // FileInfo is thread safe.
            FileInfo fileInfo = new FileInfo(cachePath);

            if (!fileInfo.Exists)
            {
                return null;
            }

            // Pull the latest info.
            fileInfo.Refresh();

            return new CachedImage
            {
                Key = Path.GetFileNameWithoutExtension(cachePath),
                Path = cachePath,
                CreationTimeUtc = fileInfo.CreationTimeUtc
            };
        }
    }
}