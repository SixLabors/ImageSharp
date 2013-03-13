// -----------------------------------------------------------------------
// <copyright file="DiskCache.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Web.Config;
    #endregion

    /// <summary>
    /// Encapsulates methods to handle disk caching of images.
    /// </summary>
    internal sealed class DiskCache
    {
        #region Fields
        /// <summary>
        /// The maximum number or time a new file should be cached before checking the 
        /// cache controller and running any clearing mechanisms.
        /// </summary>
        /// <remarks>
        /// NTFS file systems can handle up to 8000 files in one directory. The Cache controller will clear out any 
        /// time we hit 6000 so if we tell the handler to run at every 1000 times an image is added to the cache we 
        /// should have a 1000 file buffer.
        /// </remarks>
        internal const int MaxRunsBeforeCacheClear = 1000;

        /// <summary>
        ///     The maximum number of days to cache files on the system for.
        /// </summary>
        internal static readonly int MaxFileCachedDuration = ImageProcessorConfig.Instance.MaxCacheDays;

        /// <summary>
        /// The maximum number of files allowed in the directory.
        /// </summary>
        /// <remarks>
        /// NTFS Folder can handle up to 8000 files in a directory. 
        /// This buffer will help us to ensure that we rarely hit anywhere near that limit.
        /// </remarks>
        private const int MaxFilesCount = 6500;

        /// <summary>
        /// The regular expression to search strings for extension changes.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"format=(jpeg|png|bmp|gif)", RegexOptions.Compiled);

        /// <summary>
        ///     The object to lock against.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        ///     The default paths for Cached folders on the server.
        /// </summary>
        private static readonly string CachePath = ImageProcessorConfig.Instance.VirtualCachePath;
        #endregion

        #region Methods
        /// <summary>
        /// Gets the full transformed cached path for the image.
        /// </summary>
        /// <param name="imagePath">The original image path.</param>
        /// <param name="imageName">The original image name.</param>
        /// <returns>The full cached path for the image.</returns>
        internal static string GetCachePath(string imagePath, string imageName)
        {
            string virtualCachePath = CachePath;
            string absoluteCachePath = HostingEnvironment.MapPath(virtualCachePath);
            string cachedPath = string.Empty;

            if (absoluteCachePath != null)
            {
                string parsedExtension = ParseExtension(imagePath);
                string fallbackExtension = imageName.Substring(imageName.LastIndexOf(".", StringComparison.Ordinal) + 1);
                string subpath = !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension : fallbackExtension;

                string cachedFileName = string.Format(
                    "{0}.{1}",
                    imagePath.ToMD5Fingerprint(),
                    !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension : fallbackExtension);
                cachedPath = Path.Combine(absoluteCachePath, subpath, cachedFileName);

                string cachedDirectory = Path.GetDirectoryName(cachedPath);

                if (cachedDirectory != null)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(cachedDirectory);

                    if (!directoryInfo.Exists)
                    {
                        // Create the directory.
                        Directory.CreateDirectory(cachedDirectory);
                    }
                }
            }

            return cachedPath;
        }

        /// <summary>
        /// Adds an image to the cache.
        /// </summary>
        /// <param name="cachedPath">
        /// The cached path.
        /// </param>
        /// <param name="lastWriteTimeUtc">
        /// The last write time.
        /// </param>
        internal static void AddImageToCache(string cachedPath, DateTime lastWriteTimeUtc)
        {
            string key = Path.GetFileNameWithoutExtension(cachedPath);
            DateTime expires = lastWriteTimeUtc.AddDays(MaxFileCachedDuration).ToUniversalTime();
            CachedImage cachedImage = new CachedImage(key, lastWriteTimeUtc, expires);
            PersistantDictionary.Instance.Add(key, cachedImage);
        }

        /// <summary>
        /// Converts an absolute file path 
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert.</param>
        /// <param name="request">The <see cref="T:System.Web.HttpRequest"/>from the current context.</param>
        /// <returns>The virtual path to the file.</returns>
        internal static string GetVirtualPath(string absolutePath, HttpRequest request)
        {
            string applicationPath = request.PhysicalApplicationPath;
            string virtualDir = request.ApplicationPath;
            virtualDir = virtualDir == "/" ? virtualDir : (virtualDir + "/");
            if (applicationPath != null)
            {
                return absolutePath.Replace(applicationPath, virtualDir).Replace(@"\", "/");
            }

            throw new InvalidOperationException("We can only map an absolute back to a relative path if the application path is available.");
        }

        /// <summary>
        /// Purges any files from the file-system cache in a background thread.
        /// </summary>
        internal static void PurgeCachedFolders()
        {
            ThreadStart threadStart = PurgeFolders;

            Thread thread = new Thread(threadStart)
            {
                IsBackground = true
            };

            thread.Start();
        }

        /// <summary>
        /// Returns a value indicating whether the original file has been updated.
        /// </summary>
        /// <param name="imagePath">The original image path.</param>
        /// <param name="cachedImagePath">The cached image path.</param>
        /// <returns>
        /// True if the the original file has been updated; otherwise, false.
        /// </returns>
        internal static bool IsUpdatedFile(string imagePath, string cachedImagePath)
        {
            if (File.Exists(imagePath))
            {
                CachedImage image;
                string key = Path.GetFileNameWithoutExtension(cachedImagePath);
                PersistantDictionary.Instance.TryGetValue(key, out image);
                FileInfo imageFileInfo = new FileInfo(imagePath);

                return image != null && imageFileInfo.LastWriteTimeUtc.Equals(image.LastWriteTimeUtc);
            }

            return true;
        }

        /// <summary>
        /// Sets the LastWriteTime of the cached file to match the original file.
        /// </summary>
        /// <param name="imagePath">
        /// The original image path.
        /// </param>
        /// <param name="cachedImagePath">
        /// The cached image path.
        /// </param>
        /// <returns>
        /// The <see cref="System.DateTime"/> set to the last write time of the file.
        /// </returns>
        internal static DateTime SetCachedLastWriteTime(string imagePath, string cachedImagePath)
        {
            lock (SyncRoot)
            {
                if (File.Exists(imagePath) && File.Exists(cachedImagePath))
                {
                    DateTime dateTime = File.GetLastWriteTimeUtc(imagePath);
                    File.SetLastWriteTimeUtc(cachedImagePath, dateTime);
                    return dateTime;
                }
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// Purges any files from the file-system cache in the given folders.
        /// </summary>
        private static void PurgeFolders()
        {
            Regex searchTerm = new Regex(@"(jpeg|png|bmp|gif)");
            var list = PersistantDictionary.Instance.ToList()
                .GroupBy(x => searchTerm.Match(x.Value.Path))
                .Select(y => new
                                 {
                                     Path = y.Key,
                                     Expires = y.Select(z => z.Value.ExpiresUtc),
                                     Count = y.Sum(z => z.Key.Count())
                                 })
                .AsEnumerable();

            foreach (var path in list)
            {

            }



            string folder = HostingEnvironment.MapPath(CachePath);

            if (folder != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folder);

                if (directoryInfo.Exists)
                {
                    List<DirectoryInfo> directoryInfos = directoryInfo
                        .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                        .ToList();

                    Parallel.ForEach(
                        directoryInfos,
                        subDirectoryInfo =>
                        {
                            // Get all the files in the cache ordered by LastAccessTime - oldest first.
                            List<FileInfo> fileInfos = subDirectoryInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                                .OrderBy(x => x.LastAccessTimeUtc).ToList();

                            int counter = fileInfos.Count;

                            Parallel.ForEach(
                                fileInfos,
                                fileInfo =>
                                {
                                    // Delete the file if we are nearing our limit buffer.
                                    if (counter >= MaxFilesCount || fileInfo.LastAccessTimeUtc < DateTime.UtcNow.AddDays(-MaxFileCachedDuration))
                                    {
                                        lock (SyncRoot)
                                        {
                                            try
                                            {
                                                // Remove from the cache.
                                                string key = Path.GetFileNameWithoutExtension(fileInfo.Name);
                                                CachedImage cachedImage;

                                                if (PersistantDictionary.Instance.TryGetValue(key, out cachedImage))
                                                {
                                                    if (PersistantDictionary.Instance.TryRemove(key, out cachedImage))
                                                    {
                                                        fileInfo.Delete();
                                                        counter -= 1;
                                                    }
                                                }
                                            }
                                            catch (IOException)
                                            {
                                                // Do Nothing, skip to the next.                                           
                                                // TODO: Should we handle this?
                                            }
                                        }
                                    }
                                });
                        });
                }
            }
        }

        /// <summary>
        /// Returns the correct file extension for the given string input
        /// </summary>
        /// <param name="input">
        /// The string to parse.
        /// </param>
        /// <returns>
        /// The correct file extension for the given string input if it can find one; otherwise an empty string.
        /// </returns>
        private static string ParseExtension(string input)
        {
            foreach (Match match in FormatRegex.Matches(input))
            {
                if (match.Success)
                {
                    return match.Value.Split('=')[1];
                }
            }

            return string.Empty;
        }
        #endregion
    }
}
