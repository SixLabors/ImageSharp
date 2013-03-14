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
        ///     The maximum number of days to cache files on the system for.
        /// </summary>
        internal static readonly int MaxFileCachedDuration = ImageProcessorConfig.Instance.MaxCacheDays;

        /// <summary>
        /// The maximum number of files allowed in the directory.
        /// </summary>
        /// <remarks>
        /// NTFS directories can handle up to 8000 files in the directory before slowing down. 
        /// This buffer will help us to ensure that we rarely hit anywhere near that limit.
        /// </remarks>
        private const int MaxFilesCount = 7500;

        /// <summary>
        /// The regular expression to search strings for extension changes.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"(jpeg|png|bmp|gif)", RegexOptions.RightToLeft | RegexOptions.Compiled);

        /// <summary>
        /// The default paths for Cached folders on the server.
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
            DateTime expires = DateTime.UtcNow.AddDays(MaxFileCachedDuration).ToUniversalTime();
            CachedImage cachedImage = new CachedImage(cachedPath, MaxFileCachedDuration, lastWriteTimeUtc, expires);
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
        /// Returns a value indicating whether the original file has been updated.
        /// </summary>
        /// <param name="imagePath">The original image path.</param>
        /// <param name="cachedImagePath">The cached image path.</param>
        /// <returns>
        /// True if the the original file has been updated; otherwise, false.
        /// </returns>
        internal static bool IsUpdatedFile(string imagePath, string cachedImagePath)
        {
            string key = Path.GetFileNameWithoutExtension(cachedImagePath);
            bool isUpdated = false;

            if (File.Exists(imagePath))
            {
                FileInfo imageFileInfo = new FileInfo(imagePath);
                CachedImage cachedImage;

                if (PersistantDictionary.Instance.TryGetValue(key, out cachedImage))
                {
                    // Check to see if the last write time is different of whether the
                    // cached image is set to expire or if the max age is different.
                    if (imageFileInfo.LastWriteTimeUtc != cachedImage.LastWriteTimeUtc
                        || cachedImage.ExpiresUtc < DateTime.UtcNow.AddDays(-MaxFileCachedDuration)
                        || cachedImage.MaxAge != MaxFileCachedDuration)
                    {
                        if (PersistantDictionary.Instance.TryRemove(key, out cachedImage))
                        {
                            isUpdated = true;
                        }
                    }
                }
            }

            return isUpdated;
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
            if (File.Exists(imagePath) && File.Exists(cachedImagePath))
            {
                DateTime dateTime = File.GetLastWriteTimeUtc(imagePath);
                File.SetLastWriteTimeUtc(cachedImagePath, dateTime);
                return dateTime;
            }

            return DateTime.MinValue.ToUniversalTime();
        }

        /// <summary>
        /// Purges any files from the file-system cache in the given folders.
        /// </summary>
        internal static void TrimCachedFolders()
        {
            // Group each cache folder and clear any expired items or any that exeed
            // the maximum allowable count.
            var groups = PersistantDictionary.Instance.ToList()
                .GroupBy(x => FormatRegex.Match(x.Value.Path).Value)
                .Where(g => g.Count() > MaxFilesCount);

            foreach (var group in groups)
            {
                int groupCount = group.Count();

                foreach (KeyValuePair<string, CachedImage> pair in group.OrderBy(x => x.Value.ExpiresUtc))
                {
                    // If the group count is equal to the max count minus 1 then we know we
                    // are counting down from a full directory not simply clearing out 
                    // expired items.
                    if (groupCount == MaxFilesCount - 1)
                    {
                        break;
                    }

                    try
                    {
                        // Remove from the cache and delete each CachedImage.
                        FileInfo fileInfo = new FileInfo(pair.Value.Path);
                        string key = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        CachedImage cachedImage;

                        if (PersistantDictionary.Instance.TryRemove(key, out cachedImage))
                        {
                            fileInfo.Delete();
                            groupCount -= 1;
                        }
                    }
                    catch (Exception)
                    {
                        // Do Nothing, skip to the next.
                        // TODO: Should we handle this?               
                        continue;
                    }
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
            Match match = FormatRegex.Match(input);

            return match.Success ? match.Value : string.Empty;
        }

        #endregion
    }
}
