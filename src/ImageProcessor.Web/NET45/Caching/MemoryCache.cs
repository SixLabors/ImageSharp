// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MemoryCache.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// Represents an in memory collection of keys and values whose operations are concurrent.
    /// </summary>
    internal sealed class MemoryCache
    {
        #region Fields
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="T:ImageProcessor.Web.Caching.MemoryCache"/> class.
        /// initialized lazily.
        /// </summary>
        private static readonly Lazy<MemoryCache> Lazy =
                        new Lazy<MemoryCache>(() => new MemoryCache());

        /// <summary>
        /// The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();
        #endregion

        #region Constructors
        /// <summary>
        /// Prevents a default instance of the <see cref="T:ImageProcessor.Web.Caching.MemoryCache"/> class 
        /// from being created. 
        /// </summary>
        private MemoryCache()
        {
            this.LoadCache();
        }
        #endregion

        /// <summary>
        /// Gets the current instance of the <see cref="T:ImageProcessor.Web.Caching.MemoryCache"/> class.
        /// </summary>
        public static MemoryCache Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        #region Public
        /// <summary>
        /// Gets the <see cref="CachedImage"/> associated with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key of the value to get.
        /// </param>
        /// <returns>
        /// The <see cref="CachedImage"/> matching the given key if the <see cref="MemoryCache"/> contains an element with 
        /// the specified key; otherwise, null.
        /// </returns>
        public async Task<CachedImage> GetValueAsync(string key)
        {
            CachedImage cachedImage = (CachedImage)CacheManager.GetItem(key);

            if (cachedImage == null)
            {
                cachedImage = await SQLContext.GetImageAsync(key);

                if (cachedImage != null)
                {
                    CacheManager.AddItem(key, cachedImage);
                }
            }

            return cachedImage;
        }

        /// <summary>
        /// Removes the value associated with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key of the item to remove.
        /// </param>
        /// <returns>
        /// true if the <see cref="MemoryCache"/> removes an element with 
        /// the specified key; otherwise, false.
        /// </returns>
        public async Task<bool> RemoveAsync(string key)
        {
            if (await this.SaveCacheAsync(key, null, true) > 0)
            {
                CacheManager.RemoveItem(key);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary or returns the value if it exists.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="cachedImage">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The value of the item to add or get.
        /// </returns>
        public async Task<CachedImage> AddAsync(string key, CachedImage cachedImage)
        {
            // Add the CachedImage.
            if (await this.SaveCacheAsync(key, cachedImage, false) > 0)
            {
                CacheManager.AddItem(key, cachedImage);
            }

            return cachedImage;
        }
        #endregion

        /// <summary>
        /// Saves the in memory cache to the file-system.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="cachedImage">
        /// The cached Image.
        /// </param>
        /// <param name="remove">
        /// The remove.
        /// </param>
        /// <returns>
        /// true, if the dictionary is saved to the file-system; otherwise, false.
        /// </returns>
        private async Task<int> SaveCacheAsync(string key, CachedImage cachedImage, bool remove)
        {
            try
            {
                if (remove)
                {
                    return await SQLContext.RemoveImageAsync(key);
                }

                return await SQLContext.AddImageAsync(cachedImage);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Loads the cache file to populate the in memory cache.
        /// </summary>
        private void LoadCache()
        {
            lock (SyncRoot)
            {
                SQLContext.CreateDatabase();
            }
        }
    }
}