// -----------------------------------------------------------------------
// <copyright file="PersistantDictionary.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.IO;
    using System.Web.Hosting;
    using ImageProcessor.Web.Config;
    #endregion

    /// <summary>
    /// Represents a collection of keys and values whose operations are concurrent.
    /// </summary>
    public class PersistantDictionary : LockedDictionary<string, CachedImage>
    {
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="T:ImageProcessor.Web.Caching.PersistantDictionary"/> class.
        /// initialized lazily.
        /// </summary>
        private static readonly Lazy<PersistantDictionary> Lazy =
                        new Lazy<PersistantDictionary>(() => new PersistantDictionary());

        /// <summary>
        /// The default path for cached folders on the server.
        /// </summary>
        private static readonly string CachePath = ImageProcessorConfig.Instance.VirtualCachePath;

        /// <summary>
        /// The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The cached index location.
        /// </summary>
        private readonly string cachedIndexFile = Path.Combine(HostingEnvironment.MapPath(CachePath), "imagecache.bin");

        #region Constructors
        /// <summary>
        /// Prevents a default instance of the <see cref="T:ImageProcessor.Web.Caching.PersistantDictionary"/> class 
        /// from being created. 
        /// </summary>
        private PersistantDictionary()
        {
            this.LoadCache();
        }
        #endregion

        /// <summary>
        /// Gets the current instance of the <see cref="T:ImageProcessor.Web.Caching.PersistantDictionary"/> class.
        /// </summary>
        public static PersistantDictionary Instance
        {
            get
            {
                return Lazy.Value;
            }
        }

        #region Public
        /// <summary>
        /// Tries to remove the value associated with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key of the item to remove.
        /// </param>
        /// <param name="value">
        /// The value to assign the returned value to.
        /// </param>
        /// <returns>
        /// true if the <see cref="PersistantDictionary"/> removes an element with 
        /// the specified key; otherwise, false.
        /// </returns>
        public bool TryRemove(string key, out CachedImage value)
        {
            // No CachedImage to remove.
            if (!this.ContainsKey(key))
            {
                value = default(CachedImage);
                return false;
            }

            // Remove the CachedImage.
            lock (SyncRoot)
            {
                value = this[key];
                this.Remove(key);

                this.SaveCache();

                return true;
            }
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary or returns the value if it exists.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="factory">
        /// The delegate method that returns the value.
        /// </param>
        /// <returns>
        /// The value of the item to add or get.
        /// </returns>
        public CachedImage GetOrAdd(string key, Func<string, CachedImage> factory)
        {
            // Get the CachedImage.
            if (this.ContainsKey(key))
            {
                return this[key];
            }

            lock (SyncRoot)
            {
                // Add the CachedImage.
                CachedImage ret = factory(key);
                this[key] = ret;

                this.SaveCache();

                return ret;
            }
        }
        #endregion

        /// <summary>
        /// Saves the in memory cache to the file-system.
        /// </summary>
        private void SaveCache()
        {
            using (FileStream fileStream = File.Create(this.cachedIndexFile))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    // Put the count.
                    binaryWriter.Write(this.Count);

                    // Put the values.
                    foreach (var pair in this)
                    {
                        binaryWriter.Write(pair.Key);
                        binaryWriter.Write(pair.Value.ValueAndLastWriteTimeUtcToString());
                    }
                }
            }
        }

        /// <summary>
        /// Loads the cache file to populate the in memory cache.
        /// </summary>
        private void LoadCache()
        {
            lock (SyncRoot)
            {
                if (File.Exists(this.cachedIndexFile))
                {
                    using (FileStream fileStream = File.OpenRead(this.cachedIndexFile))
                    {
                        using (BinaryReader binaryReader = new BinaryReader(fileStream))
                        {
                            // Get the count.
                            int count = binaryReader.ReadInt32();

                            // Read in all pairs.
                            for (int i = 0; i < count; i++)
                            {
                                // Read the key/value strings
                                string key = binaryReader.ReadString();
                                string value = binaryReader.ReadString();

                                // Create a CachedImage
                                string[] valueAndLastWriteTime = value.Split(new[] { CachedImage.ValueLastWriteTimeDelimiter }, StringSplitOptions.None);
                                DateTime lastWriteTime = DateTime.Parse(valueAndLastWriteTime[1]);
                                CachedImage cachedImage = new CachedImage(valueAndLastWriteTime[0], lastWriteTime);
                                
                                // Assign the value
                                this[key] = cachedImage;
                            }
                        }
                    }
                }
            }
        }
    }
}