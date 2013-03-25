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
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web.Hosting;
    using ImageProcessor.Web.Config;
    using ImageProcessor.Web.Helpers;
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
        private static readonly string ConnectionString = string.Format("Data Source={0};Version=3;", IndexLocation);
        #endregion

        #region Methods
        #region Internal
        /// <summary>
        /// Creates the database if it doesn't already exist.
        /// </summary>
        internal static void CreateDatabase()
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
                
                SQLiteConnection.CreateFile(IndexLocation);

                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        using (SQLiteCommand command = new SQLiteCommand(connection))
                        {
                            command.CommandText = @"CREATE TABLE names
                                    (Key TEXT,
                                    Path TEXT,
                                    MaxAge INTEGER,
                                    LastWriteTimeUtc TEXT,
                                    ExpiresUtc TEXT,
                                    PRIMARY KEY (Key),
                                    UNIQUE (Path));";

                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
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
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = "SELECT * FROM names;";

                        SQLiteDataReader reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            string key = reader["Key"].ToString();
                            CachedImage image = new CachedImage(
                                    reader["Path"].ToString(),
                                    int.Parse(reader["MaxAge"].ToString()),
                                    DateTime.Parse(reader["LastWriteTimeUtc"].ToString()).ToUniversalTime(),
                                    DateTime.Parse(reader["ExpiresUtc"].ToString()).ToUniversalTime());

                            dictionary.Add(key, image);
                        }
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
        /// <param name="key">
        /// The key for the cached image.
        /// </param>
        /// <param name="image">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is added; otherwise, false.
        /// </returns>
        internal static async Task<bool> AddImageAsync(string key, CachedImage image)
        {
            // Create Action delegate for AddImage.
            return await TaskHelpers.Run(() => AddImage(key, image));
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
        internal static async Task<bool> RemoveImageAsync(string key)
        {
            // Create Action delegate for RemoveImage.
            return await TaskHelpers.Run(() => RemoveImage(key));
        }
        #endregion

        #region Private
        /// <summary>
        /// Adds a cached image to the database.
        /// </summary>
        /// <param name="key">
        /// The key for the cached image.
        /// </param>
        /// <param name="image">
        /// The cached image to add.
        /// </param>
        /// <returns>
        /// The true if the addition of the cached image is added; otherwise, false.
        /// </returns>
        private static bool AddImage(string key, CachedImage image)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        using (SQLiteCommand command = new SQLiteCommand(connection))
                        {
                            command.CommandText = "INSERT INTO names VALUES(?, ?, ?, ?, ?)";

                            SQLiteParameter[] parameters = new[]
                                                               {
                                                                   new SQLiteParameter("Key", key), 
                                                                   new SQLiteParameter("Path", image.Path),
                                                                   new SQLiteParameter("MaxAge", image.MaxAge),
                                                                   new SQLiteParameter("LastWriteTimeUtc", image.LastWriteTimeUtc),
                                                                   new SQLiteParameter("ExpiresUtc", image.ExpiresUtc)
                                                               };

                            command.Parameters.AddRange(parameters);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                return true;
            }
            catch
            {
                return false;
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
        private static bool RemoveImage(string key)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        using (SQLiteCommand command = new SQLiteCommand(connection))
                        {
                            command.CommandText = "DELETE FROM names WHERE key = @searchParam;";
                            command.Parameters.Add(new SQLiteParameter("searchParam", key));
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
        #endregion
    }
}
