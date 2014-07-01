// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DiskCache.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The disk cache.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;
    using ImageProcessor.Extensions;
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
        /// The maximum number of files allowed in the directory.
        /// </summary>
        /// <remarks>
        /// NTFS directories can handle up to 10,000 files in the directory before slowing down. 
        /// This will help us to ensure that don't go over that limit.
        /// <see cref="http://stackoverflow.com/questions/197162/ntfs-performance-and-large-volumes-of-files-and-directories"/>
        /// <see cref="http://stackoverflow.com/questions/115882/how-do-you-deal-with-lots-of-small-files"/>
        /// <see cref="http://stackoverflow.com/questions/1638219/millions-of-small-graphics-files-and-how-to-overcome-slow-file-system-access-on"/>
        /// </remarks>
        private const int MaxFilesCount = 50;

        /// <summary>
        /// The absolute path to virtual cache path on the server.
        /// </summary>
        private static readonly string AbsoluteCachePath = HostingEnvironment.MapPath(ImageProcessorConfig.Instance.VirtualCachePath);

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
        internal void AddImageToCache()
        {
            string key = Path.GetFileNameWithoutExtension(this.CachedPath);
            CachedImage cachedImage = new CachedImage
                                          {
                                              Key = key,
                                              Path = this.CachedPath,
                                              CreationTimeUtc = DateTime.UtcNow
                                          };

            CacheIndexer.Add(cachedImage);
        }

        /// <summary>
        /// Returns a value indicating whether the original file is new or has been updated.
        /// </summary>
        /// <returns>
        /// True if the the original file is new or has been updated; otherwise, false.
        /// </returns>
        internal async Task<bool> IsNewOrUpdatedFileAsync()
        {
            string path = this.CachedPath;
            bool isUpdated = false;
            CachedImage cachedImage;

            if (this.isRemote)
            {
                cachedImage = await CacheIndexer.GetValueAsync(path);

                if (cachedImage != null)
                {
                    // Can't check the last write time so check to see if the cached image is set to expire 
                    // or if the max age is different.
                    if (this.IsExpired(cachedImage.CreationTimeUtc))
                    {
                        CacheIndexer.Remove(path);
                        isUpdated = true;
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
                cachedImage = await CacheIndexer.GetValueAsync(path);

                if (cachedImage == null)
                {
                    // Nothing in the cache so we should return true.
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        /// <summary>
        /// Trims a cached folder ensuring that it does not exceed the maximum file count.
        /// </summary>
        /// <param name="path">
        /// The path to the folder.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task"/>.
        /// </returns>
        internal async Task TrimCachedFolderAsync(string path)
        {
            await TaskHelpers.Run(() => this.TrimCachedFolders(path));
        }
        #endregion

        #region Private
        /// <summary>
        /// Trims a cached folder ensuring that it does not exceed the maximum file count.
        /// </summary>
        /// <param name="path">
        /// The path to the folder.
        /// </param>
        private void TrimCachedFolders(string path)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(path));
            DirectoryInfo parentDirectoryInfo = directoryInfo.Parent;

            // ReSharper disable once PossibleNullReferenceException
            foreach (DirectoryInfo enumerateDirectory in SafeEnumerateDirectories(parentDirectoryInfo))
            {
                IEnumerable<FileInfo> files = enumerateDirectory.EnumerateFiles().OrderBy(f => f.CreationTimeUtc);
                int count = files.Count();

                foreach (FileInfo fileInfo in files)
                {
                    try
                    {
                        // If the group count is equal to the max count minus 1 then we know we
                        // have reduced the number of items below the maximum allowed.
                        // We'll cleanup any orphaned expired files though.
                        if (!this.IsExpired(fileInfo.CreationTimeUtc) && count <= MaxFilesCount - 1)
                        {
                            break;
                        }

                        // Remove from the cache and delete each CachedImage.
                        CacheIndexer.Remove(fileInfo.Name);
                        fileInfo.Delete();
                        count -= 1;
                    }
                    // ReSharper disable once EmptyGeneralCatchClause
                    catch
                    {
                        // Do nothing; skip to the next file.
                    }
                }
            }
        }

        public static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(DirectoryInfo directoryInfo)
        {
            IEnumerable<DirectoryInfo> directories;

            try
            {
                directories = directoryInfo.EnumerateDirectories();
            }
            catch
            {
                return Enumerable.Empty<DirectoryInfo>();
            }

            return directories;
        }

        /// <summary>
        /// Gets the full transformed cached path for the image. 
        /// The images are stored in paths that are based upon the sha1 of their full request path
        /// taking the individual characters of the hash to determine their location.
        /// This allows us to store millions of images.
        /// </summary>
        /// <returns>The full cached path for the image.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        private string GetCachePath()
        {
            string cachedPath = string.Empty;
            string streamHash = string.Empty;

            if (AbsoluteCachePath != null)
            {
                try
                {
                    if (new Uri(this.requestPath).IsFile)
                    {
                        // Get the hash for the filestream. That way we can ensure that if the image is 
                        // updated but has the same name we will know.
                        FileInfo imageFileInfo = new FileInfo(this.requestPath);
                        if (imageFileInfo.Exists)
                        {
                            // Pull the latest info.
                            imageFileInfo.Refresh();

                            // Checking the stream itself is far too processor intensive so we make a best guess.
                            string creation = imageFileInfo.CreationTimeUtc.ToString(CultureInfo.InvariantCulture);
                            string length = imageFileInfo.Length.ToString(CultureInfo.InvariantCulture);
                            streamHash = string.Format("{0}{1}", creation, length);
                        }
                    }
                }
                catch
                {
                    streamHash = string.Empty;
                }

                // Use an sha1 hash of the full path including the querystring to create the image name. 
                // That name can also be used as a key for the cached image and we should be able to use 
                // The characters of that hash as subfolders.
                string parsedExtension = ImageHelpers.GetExtension(this.fullPath);
                string fallbackExtension = this.imageName.Substring(this.imageName.LastIndexOf(".", StringComparison.Ordinal) + 1);
                string encryptedName = (streamHash + this.fullPath).ToSHA1Fingerprint();

                // Collision rate of about 1 in 10000 for the folder structure.
                string pathFromKey = string.Join("\\", encryptedName.ToCharArray().Take(6));

                string cachedFileName = string.Format(
                    "{0}.{1}",
                    encryptedName,
                    !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension.Replace(".", string.Empty) : fallbackExtension);

                cachedPath = Path.Combine(AbsoluteCachePath, pathFromKey, cachedFileName);
            }

            return cachedPath;
        }

        /// <summary>
        /// Gets a value indicating whether the given images creation date is out with 
        /// the prescribed limit.
        /// </summary>
        /// <param name="creationDate">
        /// The creation date.
        /// </param>
        /// <returns>
        /// The true if the date is out with the limit, otherwise; false.
        /// </returns>
        private bool IsExpired(DateTime creationDate)
        {
            return creationDate.AddDays(MaxFileCachedDuration) < DateTime.UtcNow.AddDays(-MaxFileCachedDuration);
        }
        #endregion
        #endregion
    }
}