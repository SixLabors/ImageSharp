// -----------------------------------------------------------------------
// <copyright file="SQLContext.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.IO;
    using System.Web.Hosting;
    using ImageProcessor.Web.Config;
    #endregion

    /// <summary>
    /// Provides a wrapper for the SQLite functionality.
    /// </summary>
    internal sealed class SQLContext
    {
        /// <summary>
        /// The default path for cached folders on the server.
        /// </summary>
        private static readonly string VirtualCachePath = ImageProcessorConfig.Instance.VirtualCachePath;

        /// <summary>
        /// The cached index location.
        /// </summary>
        private static readonly string IndexLocation = Path.Combine(HostingEnvironment.MapPath(VirtualCachePath), "imagecache.sqlite");

        /// <summary>
        /// The connection string.
        /// </summary>
        private static readonly string ConnectionString = string.Format("Data Source={0};Version=3;", IndexLocation);

        /// <summary>
        /// Creates the database if it doesn't already exist.
        /// </summary>
        public static void CreateDatabase()
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
        public static bool AddImage(string key, CachedImage image)
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
                            command.CommandText = "INSERT INTO names VALUES(?, ?, ?, ?)";

                            SQLiteParameter[] parameters = new[]
                                                               {
                                                                   new SQLiteParameter("Key", key), 
                                                                   new SQLiteParameter("Path", image.Path),
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
            catch (Exception)
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
        public static bool RemoveImage(string key)
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
                            command.CommandText = string.Format("DELETE FROM names WHERE key = '{0}';", key);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all the images from the database.
        /// </summary>
        /// <returns>
        /// The <see cref="System.Collections.Generic.Dictionary{TKey,TVal}"/>.
        /// </returns>
        public static Dictionary<string, CachedImage> GetImages()
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
                                    DateTime.Parse(reader["LastWriteTimeUtc"].ToString()).ToUniversalTime(),
                                    DateTime.Parse(reader["ExpiresUtc"].ToString()).ToUniversalTime());

                            dictionary.Add(key, image);
                        }
                    }
                }

                return dictionary;
            }
            catch (Exception)
            {
                return new Dictionary<string, CachedImage>();
            }
        }
    }
}
