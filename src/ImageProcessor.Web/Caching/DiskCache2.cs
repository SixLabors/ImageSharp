namespace ImageProcessor.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;

    using ImageProcessor.Web.Configuration;
    using ImageProcessor.Web.Extensions;

    public class DiskCache2 : ImageCacheBase
    {
        /// <summary>
        /// The maximum number of files allowed in the directory.
        /// </summary>
        /// <remarks>
        /// NTFS directories can handle up to 10,000 files in the directory before slowing down.
        /// This will help us to ensure that don't go over that limit.
        /// <see href="http://stackoverflow.com/questions/197162/ntfs-performance-and-large-volumes-of-files-and-directories"/>
        /// <see href="http://stackoverflow.com/questions/115882/how-do-you-deal-with-lots-of-small-files"/>
        /// <see href="http://stackoverflow.com/questions/1638219/millions-of-small-graphics-files-and-how-to-overcome-slow-file-system-access-on"/>
        /// </remarks>
        private const int MaxFilesCount = 100;

        /// <summary>
        /// The max age.
        /// </summary>
        private readonly int maxAge;

        /// <summary>
        /// The virtual cache path.
        /// </summary>
        private readonly string virtualCachePath;

        /// <summary>
        /// The absolute path to virtual cache path on the server.
        /// </summary>
        private readonly string absoluteCachePath;

        /// <summary>
        /// The virtual cached path to the cached file.
        /// </summary>
        private string virtualCachedFilePath;

        public DiskCache2(string requestPath, string fullPath, string querystring)
            : base(requestPath, fullPath, querystring)
        {
            // TODO: Get from configuration.
            this.Settings = new Dictionary<string, string>();
            this.maxAge = Convert.ToInt32(this.Settings["MaxAge"]);
            this.virtualCachePath = this.Settings["VirtualCachePath"];
            this.absoluteCachePath = HostingEnvironment.MapPath(this.virtualCachePath);
        }

        /// <summary>
        /// The maximum number of days to cache files on the system for.
        /// TODO: Shift the getter source to proper config.
        /// </summary>
        public override int MaxAge
        {
            get
            {
                return this.maxAge;
            }
        }

        public override async Task<bool> IsNewOrUpdatedAsync()
        {
            string cachedFileName = await this.CreateCachedFileName();

            // Collision rate of about 1 in 10000 for the folder structure.
            // That gives us massive scope to store millions of files.
            string pathFromKey = string.Join("\\", cachedFileName.ToCharArray().Take(6));
            string virtualPathFromKey = pathFromKey.Replace(@"\", "/");
            this.CachedPath = Path.Combine(this.absoluteCachePath, pathFromKey, cachedFileName);
            this.virtualCachedFilePath = Path.Combine(this.virtualCachePath, virtualPathFromKey, cachedFileName).Replace(@"\", "/");

            bool isUpdated = false;
            CachedImage cachedImage = CacheIndexer.GetValue(this.CachedPath);

            if (cachedImage == null)
            {
                // Nothing in the cache so we should return true.
                isUpdated = true;
            }
            else
            {
                // Check to see if the cached image is set to expire.
                if (this.IsExpired(cachedImage.CreationTimeUtc))
                {
                    CacheIndexer.Remove(this.CachedPath);
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        public override async Task AddImageToCacheAsync(Stream stream)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(this.CachedPath));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            using (FileStream fileStream = File.Create(this.CachedPath))
            {
                await stream.CopyToAsync(fileStream);
            }
        }

        public override async Task TrimCacheAsync()
        {
            string directory = Path.GetDirectoryName(this.CachedPath);

            if (directory != null)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                DirectoryInfo parentDirectoryInfo = directoryInfo.Parent;

                if (parentDirectoryInfo != null)
                {
                    // UNC folders can throw exceptions if the file doesn't exist.
                    foreach (DirectoryInfo enumerateDirectory in await parentDirectoryInfo.SafeEnumerateDirectoriesAsync())
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
            }
        }

        public override void RewritePath(HttpContext context)
        {
            // The cached file is valid so just rewrite the path.
            context.RewritePath(this.virtualCachedFilePath, false);
        }
    }
}
