namespace ImageProcessor.Web.AzureBlobCache
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Extensions;
    using ImageProcessor.Web.Helpers;

    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobCache : ImageCacheBase
    {
        /// <summary>
        /// The max age.
        /// </summary>
        private readonly int maxAge;

        private CloudStorageAccount cloudCachedStorageAccount;

        private CloudStorageAccount cloudSourceStorageAccount;

        private CloudBlobClient cloudCachedBlobClient;

        private CloudBlobClient cloudSourceBlobClient;

        private CloudBlobContainer cloudCachedBlobContainer;

        private CloudBlobContainer cloudSourceBlobContainer;

        private string cachedContainerRoot;

        /// <summary>
        /// The physical cached path.
        /// </summary>
        private string physicalCachedPath;

        public AzureBlobCache(string requestPath, string fullPath, string querystring)
            : base(requestPath, fullPath, querystring)
        {
            // TODO: Get from configuration.
            this.Settings = new Dictionary<string, string>();

            this.maxAge = Convert.ToInt32(this.Settings["MaxAge"]);

            // Retrieve storage accounts from connection string.
            this.cloudCachedStorageAccount = CloudStorageAccount.Parse(this.Settings["CachedStorageAccount"]);
            this.cloudSourceStorageAccount = CloudStorageAccount.Parse(this.Settings["SourceStorageAccount"]);

            // Create the blob clients.
            this.cloudCachedBlobClient = this.cloudCachedStorageAccount.CreateCloudBlobClient();
            this.cloudSourceBlobClient = this.cloudSourceStorageAccount.CreateCloudBlobClient();

            // Retrieve references to a previously created containers.
            this.cloudCachedBlobContainer = this.cloudCachedBlobClient.GetContainerReference(this.Settings["CachedBlobContainer"]);
            this.cloudSourceBlobContainer = this.cloudSourceBlobClient.GetContainerReference(this.Settings["SourceBlobContainer"]);

            this.cachedContainerRoot = this.Settings["CachedContainerRoot"];
        }

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
            this.CachedPath = Path.Combine(this.cachedContainerRoot, pathFromKey, cachedFileName).Replace(@"\", "/");

            ICloudBlob blockBlob = await this.cloudCachedBlobContainer
                                             .GetBlobReferenceFromServerAsync(this.RequestPath);

            bool isUpdated = false;
            if (!await blockBlob.ExistsAsync())
            {
                // Nothing in the cache so we should return true.
                isUpdated = true;
            }
            else
            {
                // Pull the latest info.
                await blockBlob.FetchAttributesAsync();
                if (blockBlob.Properties.LastModified.HasValue)
                {
                    // Check to see if the cached image is set to expire.
                    if (this.IsExpired(blockBlob.Properties.LastModified.Value.UtcDateTime))
                    {
                        isUpdated = true;
                    }
                }
            }

            return isUpdated;
        }

        public override async Task AddImageToCacheAsync(Stream stream)
        {
            CloudBlockBlob blockBlob = this.cloudCachedBlobContainer.GetBlockBlobReference(this.CachedPath);
            await blockBlob.UploadFromStreamAsync(stream);
        }

        public override async Task TrimCacheAsync()
        {
            Uri uri = new Uri(this.CachedPath);
            string path = uri.GetLeftPart(UriPartial.Path);
            string directory = path.Substring(0, path.LastIndexOf('/'));
            string parent = directory.Substring(0, path.LastIndexOf('/'));

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
                if (blob.Properties.LastModified.HasValue && !this.IsExpired(blob.Properties.LastModified.Value.UtcDateTime))
                {
                    await blob.DeleteAsync();
                }
            }
        }

        public override async Task<string> CreateCachedFileName()
        {
            string streamHash = string.Empty;

            try
            {
                if (new Uri(this.RequestPath).IsFile)
                {
                    ICloudBlob blockBlob = await this.cloudSourceBlobContainer
                                                     .GetBlobReferenceFromServerAsync(this.RequestPath);

                    if (await blockBlob.ExistsAsync())
                    {
                        // Pull the latest info.
                        await blockBlob.FetchAttributesAsync();

                        if (blockBlob.Properties.LastModified.HasValue)
                        {
                            string creation = blockBlob.Properties.LastModified.Value.UtcDateTime.ToString(CultureInfo.InvariantCulture);
                            string length = blockBlob.Properties.Length.ToString(CultureInfo.InvariantCulture);
                            streamHash = string.Format("{0}{1}", creation, length);
                        }
                    }
                    else
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
            // The cached file is valid so just rewrite the path.
            context.RewritePath(this.CachedPath, false);
        }
    }
}
