// -----------------------------------------------------------------------
// <copyright file="DiskCache.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using ImageProcessor.Helpers.Extensions;
    using ImageProcessor.Web.Config;
    using ImageProcessor.Web.Helpers;
    #endregion

    /// <summary>
    /// The disk cache.
    /// </summary>
    internal sealed class DiskCache
    {
        #region Fields
        /// <summary>
        /// The maximum number of days to cache files on the system for.
        /// </summary>
        internal static readonly int MaxFileCachedDuration = ImageProcessorConfig.Instance.MaxCacheDays;

        /// <summary>
        /// The valid sub directory chars. This used in combination with the file limit per folder
        /// allows the storage of 360,000 image files in the cache.
        /// </summary>
        private const string ValidSubDirectoryChars = "abcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>
        /// The maximum number of files allowed in the directory.
        /// </summary>
        /// <remarks>
        /// NTFS directories can handle up to 10,000 files in the directory before slowing down. 
        /// This will help us to ensure that don't go over that limit.
        /// <see cref="http://stackoverflow.com/questions/197162/ntfs-performance-and-large-volumes-of-files-and-directories"/>
        /// <see cref="http://stackoverflow.com/questions/115882/how-do-you-deal-with-lots-of-small-files"/>
        /// <see cref="http://stackoverflow.com/questions/1638219/millions-of-small-graphics-files-and-how-to-overcome-slow-file-system-access-on"/>
        /// </remarks>
        private const int MaxFilesCount = 10000;

        /// <summary>
        /// The regular expression to search strings for file extensions.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(
            @"(jpeg|png|bmp|gif)", RegexOptions.RightToLeft | RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for valid subfolder names.
        /// We're specifically not using a shorter regex as we need to be able to iterate through
        /// each match group.
        /// </summary>
        private static readonly Regex SubFolderRegex =
            new Regex(
                @"(\/([a-z]|[0-9])\/(a|b|c|d|e|f|g|h|i|j|k|l|m|n|o|p|q|r|s|t|u|v|w|x|y|z|0|1|2|3|4|5|6|7|8|9)\/)",
                RegexOptions.Compiled);

        /// <summary>
        /// The absolute path to virtual cache path on the server.
        /// </summary>
        private static readonly string AbsoluteCachePath =
            HostingEnvironment.MapPath(ImageProcessorConfig.Instance.VirtualCachePath);

        /// <summary>
        /// The request for the image.
        /// </summary>
        private readonly HttpRequest request;

        /// <summary>
        /// The request path for the image.
        /// </summary>
        private readonly string requestPath;

        /// <summary>
        /// The full path for the image.
        /// </summary>
        private readonly string fullPath;

        /// <summary>
        /// The image name
        /// </summary>
        private readonly string imageName;

        /// <summary>
        /// Whether the request is for a remote image.
        /// </summary>
        private readonly bool isRemote;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="DiskCache"/> class.
        /// </summary>
        /// <param name="request">
        /// The request for the image.
        /// </param>
        /// <param name="requestPath">
        /// The request path for the image.
        /// </param>
        /// <param name="fullPath">
        /// The full path for the image.
        /// </param>
        /// <param name="imageName">
        /// The image name.
        /// </param>
        /// <param name="isRemote">
        /// Whether the request is for a remote image.
        /// </param>
        public DiskCache(HttpRequest request, string requestPath, string fullPath, string imageName, bool isRemote)
        {
            this.request = request;
            this.requestPath = requestPath;
            this.fullPath = fullPath;
            this.imageName = imageName;
            this.isRemote = isRemote;
            this.CachedPath = this.GetCachePath();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the cached path.
        /// </summary>
        internal string CachedPath { get; private set; }
        #endregion

        #region Methods
        #region Internal
        /// <summary>
        /// Creates the series of directories required to house our cached images.
        /// The images are stored in paths that are based upon the MD5 of their full request path
        /// taking the first and last characters of the hash to determine their location.
        /// <example>~/cache/a/1/ab04g67p91.jpg</example>
        /// This allows us to store 36 folders within 36 folders giving us a total of 12,960,000 images.
        /// </summary>
        /// <returns>
        /// True if the directories are successfully created; otherwise, false.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        internal static bool CreateDirectories()
        {
            bool success = true;

            try
            {
                // Split up our characters into an array to loop though.
                char[] characters = ValidSubDirectoryChars.ToCharArray();

                // Loop through and create the first level.
                Parallel.ForEach(
                    characters,
                    (character, loop) =>
                    {
                        string firstSubPath = Path.Combine(AbsoluteCachePath, character.ToString(CultureInfo.InvariantCulture));
                        DirectoryInfo directoryInfo = new DirectoryInfo(firstSubPath);

                        if (!directoryInfo.Exists)
                        {
                            directoryInfo.Create();

                            // Loop through and create the second level.
                            Parallel.ForEach(
                                characters,
                                (subCharacter, subLoop) =>
                                {
                                    string secondSubPath = Path.Combine(firstSubPath, subCharacter.ToString(CultureInfo.InvariantCulture));
                                    DirectoryInfo subDirectoryInfo = new DirectoryInfo(secondSubPath);

                                    if (!subDirectoryInfo.Exists)
                                    {
                                        subDirectoryInfo.Create();
                                    }
                                });
                        }
                    });
            }
            catch
            {
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Gets the virtual path to the cached processed image.
        /// </summary>
        /// <returns>The virtual path to the cached processed image.</returns>
        internal string GetVirtualCachedPath()
        {
            string applicationPath = this.request.PhysicalApplicationPath;
            string virtualDir = this.request.ApplicationPath;
            virtualDir = virtualDir == "/" ? virtualDir : (virtualDir + "/");

            if (applicationPath != null)
            {
                return this.CachedPath.Replace(applicationPath, virtualDir).Replace(@"\", "/");
            }

            throw new InvalidOperationException(
                "We can only map an absolute back to a relative path if the application path is available.");
        }

        /// <summary>
        /// Adds an image to the cache.
        /// </summary>
        /// <param name="lastWriteTimeUtc">
        /// The last write time.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task"/>.
        /// </returns>
        internal async Task AddImageToCacheAsync(DateTime lastWriteTimeUtc)
        {
            string key = Path.GetFileNameWithoutExtension(this.CachedPath);
            DateTime expires = DateTime.UtcNow.AddDays(MaxFileCachedDuration).ToUniversalTime();
            CachedImage cachedImage = new CachedImage
                                          {
                                              Key = key,
                                              Path = this.CachedPath,
                                              MaxAge = MaxFileCachedDuration,
                                              LastWriteTimeUtc = lastWriteTimeUtc,
                                              ExpiresUtc = expires
                                          };

            await PersistantDictionary.Instance.AddAsync(key, cachedImage);
        }

        /// <summary>
        /// Returns a value indicating whether the original file is new or has been updated.
        /// </summary>
        /// <returns>
        /// True if the the original file is new or has been updated; otherwise, false.
        /// </returns>
        internal async Task<bool> IsNewOrUpdatedFileAsync()
        {
            string key = Path.GetFileNameWithoutExtension(this.CachedPath);
            CachedImage cachedImage;
            bool isUpdated = false;

            if (this.isRemote)
            {
                if (PersistantDictionary.Instance.TryGetValue(key, out cachedImage))
                {
                    // Can't check the last write time so check to see if the cached image is set to expire 
                    // or if the max age is different.
                    if (cachedImage.ExpiresUtc < DateTime.UtcNow.AddDays(-MaxFileCachedDuration)
                        || cachedImage.MaxAge != MaxFileCachedDuration)
                    {
                        if (await PersistantDictionary.Instance.TryRemoveAsync(key))
                        {
                            isUpdated = true;
                        }
                    }
                }
                else
                {
                    // Nothing in the cache so we should return true.
                    isUpdated = true;
                }
            }
            else
            {
                // Test now for locally requested files.
                if (PersistantDictionary.Instance.TryGetValue(key, out cachedImage))
                {
                    FileInfo imageFileInfo = new FileInfo(this.requestPath);

                    if (imageFileInfo.Exists)
                    {
                        // Check to see if the last write time is different of whether the
                        // cached image is set to expire or if the max age is different.
                        if (!this.RoughDateTimeCompare(imageFileInfo.LastWriteTimeUtc, cachedImage.LastWriteTimeUtc)
                            || cachedImage.ExpiresUtc < DateTime.UtcNow.AddDays(-MaxFileCachedDuration)
                            || cachedImage.MaxAge != MaxFileCachedDuration)
                        {
                            if (await PersistantDictionary.Instance.TryRemoveAsync(key))
                            {
                                isUpdated = true;
                            }
                        }
                    }
                }
                else
                {
                    // Nothing in the cache so we should return true.
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        /// <summary>
        /// Sets the LastWriteTime of the cached file to match the original file.
        /// </summary>
        /// <returns>
        /// The <see cref="T:System.DateTime"/> set to the last write time of the file.
        /// </returns>
        internal async Task<DateTime> SetCachedLastWriteTimeAsync()
        {
            // Create Action delegate for SetCachedLastWriteTime.
            return await TaskHelpers.Run(() => this.SetCachedLastWriteTime());
        }

        /// <summary>
        /// Purges any files from the file-system cache in the given folders.
        /// </summary>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task"/>.
        /// </returns>
        internal async Task TrimCachedFoldersAsync()
        {
            // Create Action delegate for TrimCachedFolders.
            await TaskHelpers.Run(this.TrimCachedFolders);
        }
        #endregion

        #region Private
        /// <summary>
        /// Sets the LastWriteTime of the cached file to match the original file.
        /// </summary>
        /// <returns>
        /// The <see cref="T:System.DateTime"/> of the original and cached file.
        /// </returns>
        private DateTime SetCachedLastWriteTime()
        {
            FileInfo cachedFileInfo = new FileInfo(this.CachedPath);
            DateTime lastWriteTime = DateTime.MinValue.ToUniversalTime();

            if (this.isRemote)
            {
                if (cachedFileInfo.Exists)
                {
                    lastWriteTime = cachedFileInfo.LastWriteTimeUtc;
                }
            }
            else
            {
                FileInfo imageFileInfo = new FileInfo(this.requestPath);

                if (imageFileInfo.Exists && cachedFileInfo.Exists)
                {
                    DateTime dateTime = imageFileInfo.LastWriteTimeUtc;
                    cachedFileInfo.LastWriteTimeUtc = dateTime;

                    lastWriteTime = dateTime;
                }
            }

            return lastWriteTime;
        }

        /// <summary>
        /// Purges any files from the file-system cache in the given folders.
        /// </summary>
        private async void TrimCachedFolders()
        {
            // Group each cache folder and clear any expired items or any that exeed
            // the maximum allowable count.
            var groups = PersistantDictionary.Instance.ToList()
                .GroupBy(x => SubFolderRegex.Match(x.Value.Path).Value)
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

                        if (await PersistantDictionary.Instance.TryRemoveAsync(key))
                        {
                            fileInfo.Delete();
                            groupCount -= 1;
                        }
                    }
                    // ReSharper disable EmptyGeneralCatchClause
                    catch
                    // ReSharper restore EmptyGeneralCatchClause
                    {
                        // Do nothing; skip to the next file.
                    }
                }
            }
        }

        /// <summary>
        /// Gets the full transformed cached path for the image. 
        /// The images are stored in paths that are based upon the MD5 of their full request path
        /// taking the first and last characters of the hash to determine their location.
        /// <example>~/cache/a/1/ab04g67p91.jpg</example>
        /// This allows us to store 36 folders within 36 folders giving us a total of 12,960,000 images.
        /// </summary>
        /// <returns>The full cached path for the image.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private string GetCachePath()
        {
            string cachedPath = string.Empty;

            if (AbsoluteCachePath != null)
            {
                // Use an md5 hash of the full path including the querystring to create the image name. 
                // That name can also be used as a key for the cached image and we should be able to use 
                // The first character of that hash as a subfolder.
                string parsedExtension = this.ParseExtension(this.fullPath);
                string fallbackExtension = this.imageName.Substring(this.imageName.LastIndexOf(".", StringComparison.Ordinal) + 1);
                string encryptedName = this.fullPath.ToMD5Fingerprint();
                string firstSubpath = encryptedName.Substring(0, 1);
                string secondSubpath = encryptedName.Substring(31, 1);

                string cachedFileName = string.Format(
                    "{0}.{1}",
                    encryptedName,
                    !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension : fallbackExtension);

                cachedPath = Path.Combine(AbsoluteCachePath, firstSubpath, secondSubpath, cachedFileName);
            }

            return cachedPath;
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
        private string ParseExtension(string input)
        {
            Match match = FormatRegex.Match(input);

            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>
        /// The rough date time compare.
        /// </summary>
        /// <param name="first">
        /// The first.
        /// </param>
        /// <param name="second">
        /// The second.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/> true if the DateTimes roughly compare; otherwise, false.
        /// </returns>
        private bool RoughDateTimeCompare(DateTime first, DateTime second)
        {
            if (first.ToString(CultureInfo.InvariantCulture) == second.ToString(CultureInfo.InvariantCulture))
            {
                return true;
            }

            return false;
        }
        #endregion
        #endregion
    }
}
