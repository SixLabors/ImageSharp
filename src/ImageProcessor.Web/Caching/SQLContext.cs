// -----------------------------------------------------------------------
// <copyright file="SQLContext.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web.Hosting;
    using ImageProcessor.Web.Config;
    using ImageProcessor.Web.Helpers;

    using SQLite;

    #endregion

    /// <summary>
    /// Provides a wrapper for the SQLite functionality.
    /// </summary>
    internal sealed class SQLContext
    {
        #region Fields
        /// <summary>
        /// The default path for cached folders on the server.
        /// </summary>
        private static readonly string VirtualCachePath = ImageProcessorConfig.Instance.VirtualCachePath;

        /// <summary>
        /// The cached index location.
        /// </summary>
        private static readonly string IndexLocation = Path.Combine(HostingEnvironment.MapPath(VirtualCachePath), "cache.db");

        /// <summary>
        /// The connection string.
        /// </summary>
        private static readonly string ConnectionString = IndexLocation;
        #endregion

        #region Methods
        #region Internal

        /// <summary>
        /// Creates the database if it doesn't already exist.
        /// </summary>
        internal static void CreateDatabase()
        {
            try
            {
                if (!File.Exists(IndexLocation))
                {
                    string absolutePath = HostingEnvironment.MapPath(VirtualCachePath);

                    if (absolutePath != null)
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(absolutePath);

                        if (!directoryInfo.Exists)
                        {
                            // Create the directory.
                            Directory.CreateDirectory(absolutePath);
                        }
                    }

                    using (SQLiteConnection connection = new SQLiteConnection(IndexLocation))
                    {
                        connection.CreateTable<CachedImage>();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Gets all the images from the database.
        /// </summary>
        /// <returns>
        /// The <see cref="System.Collections.Generic.Dictionary{TKey,TVal}"/>.
        /// </returns>
        internal static Dictionary<string, CachedImage> GetImages()
        {
            Dictionary<string, CachedImage> dictionary = new Dictionary<string, CachedImage>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    List<CachedImage> images = connection.Query<CachedImage>("SELECT * FROM CachedImage");

                    foreach (CachedImage cachedImage in images)
                    {
                        dictionary.Add(cachedImage.Key, cachedImage);
                    }
                }

                return dictionary;
            }
            catch
            {
                return new Dictionary<string, CachedImage>();
            }
        }

        /// <summary>
        /// Adds a cached image to the database.
        /// </summary>
        /// <param name="image">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is added; otherwise, false.
        /// </returns>
        internal static async Task<int> AddImageAsync(CachedImage image)
        {
            // Create Action delegate for AddImage.
            return await TaskHelpers.Run(() => AddImage(image));
        }

        /// <summary>
        /// Removes a cached image from the database.
        /// </summary>
        /// <param name="key">
        /// The key for the cached image.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is removed; otherwise, false.
        /// </returns>
        internal static async Task<int> RemoveImageAsync(string key)
        {
            // Create Action delegate for RemoveImage.
            return await TaskHelpers.Run(() => RemoveImage(key));
        }
        #endregion

        #region Private

        /// <summary>
        /// Adds a cached image to the database.
        /// </summary>
        /// <param name="image">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is added; otherwise, false.
        /// </returns>
        private static int AddImage(CachedImage image)
        {
            try
            {
                int id = 0;
                SQLiteConnection connection = new SQLiteConnection(ConnectionString);

                id = connection.Insert(image);
                connection.Dispose();

                return id;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Removes a cached image from the database.
        /// </summary>
        /// <param name="key">
        /// The key for the cached image.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is removed; otherwise, false.
        /// </returns>
        private static int RemoveImage(string key)
        {
            try
            {
                int id = 0;
                SQLiteConnection connection = new SQLiteConnection(ConnectionString);

                id = connection.Delete<CachedImage>(key);
                connection.Dispose();

                return id;
            }
            catch
            {
                return 0;
            }
        }
        #endregion
        #endregion
    }
}
