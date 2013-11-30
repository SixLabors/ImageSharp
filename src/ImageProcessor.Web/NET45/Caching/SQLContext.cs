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
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Gets all the images from the database.
        /// </summary>
        /// <returns>
        /// The <see cref="System.Collections.Generic.List{CleanupImage}"/>.
        /// </returns>
        internal static List<CleanupImage> GetImagesForCleanup()
        {
            try
            {
                List<CleanupImage> images;
                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    images = connection.Query<CleanupImage>("SELECT Path,ExpiresUtc FROM CachedImage");
                }

                return images;

            }
            catch
            {
                return new List<CleanupImage>();
            }
        }

        /// <summary>
        /// Gets a cached image from the database.
        /// </summary>
        /// <param name="key">
        /// The key for the cached image to get.
        /// </param>
        /// <returns>
        /// The <see cref="CachedImage"/> from the database.
        /// </returns>
        internal static async Task<CachedImage> GetImageAsync(string key)
        {
            try
            {
                SQLiteAsyncConnection connection = new SQLiteAsyncConnection(ConnectionString);

                return await connection.GetAsync<CachedImage>(c => c.Key == key);
            }
            catch
            {
                return null;
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
            try
            {
                SQLiteAsyncConnection connection = new SQLiteAsyncConnection(ConnectionString);
                return await connection.InsertAsync(image);
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
        internal static async Task<int> RemoveImageAsync(string key)
        {
            try
            {
                SQLiteAsyncConnection connection = new SQLiteAsyncConnection(ConnectionString);
                CachedImage cachedImage = await connection.GetAsync<CachedImage>(c => c.Key == key);

                return await connection.DeleteAsync(cachedImage);
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
