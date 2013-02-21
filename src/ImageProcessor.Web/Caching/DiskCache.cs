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
    using ImageProcessor.Web.Helpers;
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
        private const int MaxFilesCount = 6000;

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
                DirectoryInfo di = new DirectoryInfo(absoluteCachePath);

                if (!di.Exists)
                {
                    // Create the directory.
                    Directory.CreateDirectory(absoluteCachePath);
                }

                string parsedExtension = ParseExtension(imagePath);
                string fallbackExtension = imageName.Substring(imageName.LastIndexOf(".", StringComparison.Ordinal));

                string cachedFileName = string.Format(
                    "{0}{1}",
                    imagePath.ToMD5Fingerprint(),
                    !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension : fallbackExtension);
                cachedPath = Path.Combine(absoluteCachePath, cachedFileName);
            }

            return cachedPath;
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
        /// Purges any files from the filesystem cache in a background thread.
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
            if (File.Exists(imagePath) && File.Exists(cachedImagePath))
            {
                FileInfo imageFileInfo = new FileInfo(imagePath);
                FileInfo cachedImageFileInfo = new FileInfo(cachedImagePath);

                return !new FileCompareLastwritetime().Equals(imageFileInfo, cachedImageFileInfo);
            }

            return true;
        }

        /// <summary>
        /// Sets the LastWriteTime of the cached file to match the original file.
        /// </summary>
        /// <param name="imagePath">The original image path.</param>
        /// <param name="cachedImagePath">The cached image path.</param>
        internal static void SetCachedLastWriteTime(string imagePath, string cachedImagePath)
        {
            if (File.Exists(imagePath) && File.Exists(cachedImagePath))
            {
                lock (SyncRoot)
                {
                    DateTime dateTime = File.GetLastWriteTime(imagePath);
                    File.SetLastWriteTime(cachedImagePath, dateTime);
                }
            }
        }

        /// <summary>
        /// Purges any files from the filesystem cache in the given folders.
        /// </summary>
        private static void PurgeFolders()
        {
            string folder = HostingEnvironment.MapPath(CachePath);

            if (folder != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folder);

                if (directoryInfo.Exists)
                {
                    // Get all the files in the cache ordered by LastAccessTime - oldest first.
                    List<FileInfo> fileInfos = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                        .OrderBy(x => x.LastAccessTime).ToList();

                    int counter = fileInfos.Count;

                    Parallel.ForEach(
                        fileInfos,
                        fileInfo =>
                        {
                            lock (SyncRoot)
                            {
                                try
                                {
                                    // Delete the file if we are nearing our limit buffer.
                                    if (counter >= MaxFilesCount || fileInfo.LastAccessTime < DateTime.Now.AddDays(-MaxFileCachedDuration))
                                    {
                                        fileInfo.Delete();
                                        counter -= 1;
                                    }
                                }
                                catch
                                {
                                    // TODO: Sort out the try/catch.
                                    throw;
                                }
                            }
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
                    return "." + match.Value.Split('=')[1];
                }
            }

            return string.Empty;
        }
        #endregion
    }
}
