namespace ImageProcessor.Web.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobCache : ImageCacheBase
    {
        /// <summary>
        /// The max age.
        /// </summary>
        private readonly int maxDays;

        private CloudStorageAccount cloudCachedStorageAccount;

        private CloudStorageAccount cloudSourceStorageAccount;

        private CloudBlobClient cloudCachedBlobClient;

        private CloudBlobClient cloudSourceBlobClient;

        private CloudBlobContainer cloudCachedBlobContainer;

        private CloudBlobContainer cloudSourceBlobContainer;

        private string cachedCDNRoot;

        private string cachedRewritePath;

        /// <summary>
        /// The physical cached path.
        /// </summary>
        private string physicalCachedPath;

        public AzureBlobCache(string requestPath, string fullPath, string querystring)
            : base(requestPath, fullPath, querystring)
        {
            this.maxDays = Convert.ToInt32(this.Settings["MaxAge"]);

            // Retrieve storage accounts from connection string.
            this.cloudCachedStorageAccount = CloudStorageAccount.Parse(this.Settings["CachedStorageAccount"]);
            this.cloudSourceStorageAccount = CloudStorageAccount.Parse(this.Settings["SourceStorageAccount"]);

            // Create the blob clients.
            this.cloudCachedBlobClient = this.cloudCachedStorageAccount.CreateCloudBlobClient();
            this.cloudSourceBlobClient = this.cloudSourceStorageAccount.CreateCloudBlobClient();

            // Retrieve references to a previously created containers.
            this.cloudCachedBlobContainer = this.cloudCachedBlobClient.GetContainerReference(this.Settings["CachedBlobContainer"]);
            this.cloudSourceBlobContainer = this.cloudSourceBlobClient.GetContainerReference(this.Settings["SourceBlobContainer"]);

            this.cachedCDNRoot = this.Settings["CachedCDNRoot"];
        }

        public override int MaxDays
        {
            get
            {
                return this.maxDays;
            }
        }

        public override async Task<bool> IsNewOrUpdatedAsync()
        {
            string cachedFileName = await this.CreateCachedFileName();

            // Collision rate of about 1 in 10000 for the folder structure.
            // That gives us massive scope to store millions of files.
            string pathFromKey = string.Join("\\", cachedFileName.ToCharArray().Take(6));
            this.CachedPath = Path.Combine(this.cloudCachedBlobContainer.Uri.ToString(), pathFromKey, cachedFileName).Replace(@"\", "/");
            this.cachedRewritePath = Path.Combine(this.cachedCDNRoot, this.cloudCachedBlobContainer.Name, pathFromKey, cachedFileName).Replace(@"\", "/");

            bool isUpdated = false;
            CachedImage cachedImage = CacheIndexer.GetValue(this.CachedPath);

            if (new Uri(this.CachedPath).IsFile)
            {
                FileInfo fileInfo = new FileInfo(this.CachedPath);

                if (fileInfo.Exists)
                {
                    // Pull the latest info.
                    fileInfo.Refresh();

                    cachedImage = new CachedImage
                    {
                        Key = Path.GetFileNameWithoutExtension(this.CachedPath),
                        Path = this.CachedPath,
                        CreationTimeUtc = fileInfo.CreationTimeUtc
                    };

                    CacheIndexer.Add(cachedImage);
                }
            }

            if (cachedImage == null)
            {
                string blobPath = this.CachedPath.Substring(this.cloudCachedBlobContainer.Uri.ToString().Length + 1);
                CloudBlockBlob blockBlob = this.cloudCachedBlobContainer.GetBlockBlobReference(blobPath);

                if (await blockBlob.ExistsAsync())
                {
                    // Pull the latest info.
                    await blockBlob.FetchAttributesAsync();

                    if (blockBlob.Properties.LastModified.HasValue)
                    {
                        cachedImage = new CachedImage
                        {
                            Key = Path.GetFileNameWithoutExtension(this.CachedPath),
                            Path = this.CachedPath,
                            CreationTimeUtc =
                                blockBlob.Properties.LastModified.Value.UtcDateTime
                        };

                        CacheIndexer.Add(cachedImage);
                    }
                }
            }

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
            string blobPath = this.CachedPath.Substring(this.cloudCachedBlobContainer.Uri.ToString().Length + 1);
            CloudBlockBlob blockBlob = this.cloudCachedBlobContainer.GetBlockBlobReference(blobPath);
            await blockBlob.UploadFromStreamAsync(stream);
        }

        public override async Task TrimCacheAsync()
        {
            Uri uri = new Uri(this.CachedPath);
            string path = uri.GetLeftPart(UriPartial.Path);
            string directory = path.Substring(0, path.LastIndexOf('/'));
            string parent = directory.Substring(this.cloudCachedBlobContainer.Uri.ToString().Length + 1, path.LastIndexOf('/'));

            BlobContinuationToken continuationToken = null;
            CloudBlobDirectory directoryBlob = this.cloudCachedBlobContainer.GetDirectoryReference(parent);
            List<IListBlobItem> results = new List<IListBlobItem>();

            // Loop through the all the files in a non blocking fashion.
            do
            {
                BlobResultSegment response = await directoryBlob.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);

            // Now leap through and delete.
            foreach (CloudBlockBlob blob in results
                .Where((blobItem, type) => blobItem is CloudBlockBlob)
                .Cast<CloudBlockBlob>()
                .OrderBy(b => b.Properties.LastModified != null ? b.Properties.LastModified.Value.UtcDateTime : new DateTime()))
            {
                if (blob.Properties.LastModified.HasValue
                    && !this.IsExpired(blob.Properties.LastModified.Value.UtcDateTime))
                {
                    break;
                }

                // Remove from the cache and delete each CachedImage.
                CacheIndexer.Remove(blob.Name);
                await blob.DeleteAsync();
            }
        }

        public override async Task<string> CreateCachedFileName()
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
                else
                {
                    string blobPath = this.CachedPath.Substring(this.cloudSourceBlobContainer.Uri.ToString().Length + 1);
                    CloudBlockBlob blockBlob = this.cloudSourceBlobContainer.GetBlockBlobReference(blobPath);

                    if (await blockBlob.ExistsAsync())
                    {
                        // Pull the latest info.
                        await blockBlob.FetchAttributesAsync();

                        if (blockBlob.Properties.LastModified.HasValue)
                        {
                            string creation = blockBlob.Properties
                                                       .LastModified.Value.UtcDateTime
                                                       .ToString(CultureInfo.InvariantCulture);

                            string length = blockBlob.Properties.Length.ToString(CultureInfo.InvariantCulture);
                            streamHash = string.Format("{0}{1}", creation, length);
                        }
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

            return cachedFileName;
        }

        public override void RewritePath(HttpContext context)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this.cachedRewritePath);
            request.Method = "HEAD";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                HttpStatusCode responseCode = response.StatusCode;
                context.Response.Redirect(
                    responseCode == HttpStatusCode.NotFound ? this.CachedPath : this.cachedRewritePath,
                    false);
            }
        }
    }
}
