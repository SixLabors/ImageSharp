// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheManager.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods that allow the caching and retrieval of objects.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Runtime.Caching;
    #endregion

    /// <summary>
    /// Encapsulates methods that allow the caching and retrieval of objects.
    /// </summary>
    public static class CacheManager
    {
        #region Fields
        /// <summary>
        /// The cache
        /// </summary>
        private static readonly ObjectCache Cache = System.Runtime.Caching.MemoryCache.Default;

        /// <summary>
        /// An internal list of cache keys to allow bulk removal.
        /// </summary>
        private static readonly Dictionary<string, string> CacheItems = new Dictionary<string, string>();
        #endregion

        #region Methods
        /// <summary>
        /// Adds an item to the cache.
        /// </summary>
        /// <param name="key">
        /// A unique identifier for the cache entry.
        /// </param>
        /// <param name="value">
        /// The object to insert.
        /// </param>
        /// <param name="policy">
        /// Optional. An <see cref="T:System.Runtime.Caching.CacheItemPolicy"/> object that contains eviction details for the cache entry. This object
        /// provides more options for eviction than a simple absolute expiration. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <param name="regionName">
        /// Optional. A named region in the cache to which the cache entry can be added,
        /// if regions are implemented. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <returns>
        /// True if the insertion try succeeds, or false if there is an already an entry
        ///  in the cache with the same key as key.
        /// </returns>
        public static bool AddItem(string key, object value, CacheItemPolicy policy = null, string regionName = null)
        {
            bool isAdded;

            lock (Cache)
            {
                if (policy == null)
                {
                    // Create a new cache policy with the default values 
                    policy = new CacheItemPolicy();
                }

                isAdded = Cache.Add(key, value, policy, regionName);

                if (isAdded)
                {
                    CacheItems.Add(key, regionName);
                }
            }

            return isAdded;
        }

        /// <summary>
        /// Fetches an item matching the given key from the cache.
        /// </summary>
        /// <param name="key">
        /// A unique identifier for the cache entry.
        /// </param>
        /// <param name="regionName">
        /// Optional. A named region in the cache to which the cache entry can be added,
        /// if regions are implemented. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <returns>
        /// The cache entry that is identified by key.
        /// </returns>
        public static object GetItem(string key, string regionName = null)
        {
            return Cache.Get(key, regionName);
        }

        //public static bool

        /// <summary>
        /// Updates an item to the cache.
        /// </summary>
        /// <param name="key">
        /// A unique identifier for the cache entry.
        /// </param>
        /// <param name="value">
        /// The object to insert.
        /// </param>
        /// <param name="policy">
        /// Optional. An <see cref="T:System.Runtime.Caching.CacheItemPolicy"/> object that contains eviction details for the cache entry. This object
        /// provides more options for eviction than a simple absolute expiration. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <param name="regionName">
        /// Optional. A named region in the cache to which the cache entry can be added,
        /// if regions are implemented. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <returns>
        /// True if the update try succeeds, or false if there is an already an entry
        ///  in the cache with the same key as key.
        /// </returns>
        public static bool UpdateItem(string key, object value, CacheItemPolicy policy = null, string regionName = null)
        {
            bool isUpDated = true;

            // Remove the item from the cache if it already exists. MemoryCache will
            // not add an item with an existing name.
            if (GetItem(key, regionName) != null)
            {
                isUpDated = RemoveItem(key, regionName);
            }

            if (policy == null)
            {
                // Create a new cache policy with the default values 
                policy = new CacheItemPolicy();
            }

            if (isUpDated)
            {
                isUpDated = AddItem(key, value, policy, regionName);
            }

            return isUpDated;
        }

        /// <summary>
        /// Removes an item matching the given key from the cache.
        /// </summary>
        /// <param name="key">
        /// A unique identifier for the cache entry.
        /// </param>
        /// <param name="regionName">
        /// Optional. A named region in the cache to which the cache entry can be added,
        /// if regions are implemented. The default value for the optional parameter
        /// is null.
        /// </param>
        /// <returns>
        /// True if the removal try succeeds, or false if there is an already an entry
        ///  in the cache with the same key as key.
        /// </returns>
        public static bool RemoveItem(string key, string regionName = null)
        {
            bool isRemoved;

            lock (Cache)
            {
                isRemoved = Cache.Remove(key, regionName) != null;

                if (isRemoved)
                {
                    CacheItems.Remove(key);
                }
            }

            return isRemoved;
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        /// <param name="regionName">
        /// The region name.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool Clear(string regionName = null)
        {
            bool isCleared = false;

            lock (CacheItems)
            {
                // You can't remove items from a collection whilst you are iterating over it so you need to 
                // create a collection to store the items to remove.
                Dictionary<string, string> tempDictionary = new Dictionary<string, string>();

                foreach (KeyValuePair<string, string> cacheItem in CacheItems)
                {
                    // Does the cached key come with a region.
                    if ((cacheItem.Value == null) || (cacheItem.Value != null && cacheItem.Value.Equals(regionName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isCleared = RemoveItem(cacheItem.Key, cacheItem.Value);

                        if (isCleared)
                        {
                            tempDictionary.Add(cacheItem.Key, cacheItem.Value);
                        }
                    }
                }

                if (isCleared)
                {
                    // Loop through and clear out the dictionary of cache keys.
                    foreach (KeyValuePair<string, string> cacheItem in tempDictionary)
                    {
                        CacheItems.Remove(cacheItem.Key);
                    }
                }
            }

            return isCleared;
        }
        #endregion
    }
}