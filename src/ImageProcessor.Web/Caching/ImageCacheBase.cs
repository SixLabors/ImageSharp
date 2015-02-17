namespace ImageProcessor.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    public abstract class ImageCacheBase : IImageCache
    {
        /// <summary>
        /// The request path for the image.
        /// </summary>
        protected readonly string RequestPath;

        /// <summary>
        /// The full path for the image.
        /// </summary>
        protected readonly string FullPath;

        /// <summary>
        /// The querystring containing processing instructions.
        /// </summary>
        protected readonly string Querystring;

        /// <summary>
        /// The assembly version.
        /// </summary>
        private static readonly string AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCacheBase"/> class.
        /// </summary>
        /// <param name="requestPath">
        /// The request path for the image.
        /// </param>
        /// <param name="fullPath">
        /// The full path for the image.
        /// </param>
        /// <param name="querystring">
        /// The querystring containing instructions.
        /// </param>
        protected ImageCacheBase(string requestPath, string fullPath, string querystring)
        {
            this.RequestPath = requestPath;
            this.FullPath = fullPath;
            this.Querystring = querystring;
        }

        /// <summary>
        /// Gets any additional settings required by the cache.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        public string CachedPath { get; protected set; }

        public abstract int MaxAge { get; }

        public abstract Task<bool> IsNewOrUpdatedAsync();

        public abstract Task AddImageToCacheAsync(Stream stream);

        public abstract Task TrimCacheAsync();

        public virtual Task<string> CreateCachedFileName()
        {
            string streamHash = string.Empty;

            try
            {
                if (new Uri(this.RequestPath).IsFile)
                {
                    // Get the hash for the filestream. That way we can ensure that if the image is
                    // updated but has the same name we will know.
                    FileInfo imageFileInfo = new FileInfo(this.RequestPath);
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
            // The characters of that hash as sub-folders.
            string parsedExtension = ImageHelpers.GetExtension(this.FullPath, this.Querystring);
            string encryptedName = (streamHash + this.FullPath).ToSHA1Fingerprint();

            string cachedFileName = string.Format(
                 "{0}.{1}",
                 encryptedName,
                 !string.IsNullOrWhiteSpace(parsedExtension) ? parsedExtension.Replace(".", string.Empty) : "jpg");

            return Task.FromResult(cachedFileName);
        }

        public abstract void RewritePath(HttpContext context);

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
        protected virtual bool IsExpired(DateTime creationDate)
        {
            return creationDate.AddDays(this.MaxAge) < DateTime.UtcNow.AddDays(-this.MaxAge);
        }
    }
}
