namespace ImageProcessor.Web.AzureBlobCache
{
    using System;
    using System.Configuration;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Caching;

    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobCache : ImageCacheBase
    {
        private CloudStorageAccount cloudStorageAccount;

        private CloudBlobClient cloudBlobClient;

        private CloudBlobContainer cloudBlobContainer;

        public AzureBlobCache(string requestPath, string fullPath, string querystring)
            : base(requestPath, fullPath, querystring)
        {
            // TODO: These should all be in the configuration.

            // Retrieve storage account from connection string.
            this.cloudStorageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the blob client.
            this.cloudBlobClient = this.cloudStorageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            this.cloudBlobContainer = this.cloudBlobClient.GetContainerReference("mycontainer");
        }

        public override int MaxAge
        {
            get { throw new System.NotImplementedException(); }
        }

        public override async Task<bool> IsNewOrUpdatedAsync()
        {
            string cachedFileName = await this.CreateCachedFileName();

            // TODO: Generate cache path.
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(""));

            bool isUpdated = false;
            if (!await blockBlob.ExistsAsync())
            {
                // Nothing in the cache so we should return true.
                isUpdated = true;
            }
            else if (blockBlob.Properties.LastModified.HasValue)
            {
                // Check to see if the cached image is set to expire.
                if (this.IsExpired(blockBlob.Properties.LastModified.Value.UtcDateTime))
                {
                    isUpdated = true;
                }
            }

            return isUpdated;
        }

        public override async Task AddImageToCacheAsync(System.IO.Stream stream)
        {
            throw new System.NotImplementedException();
        }

        public override async Task TrimCacheAsync()
        {
            throw new System.NotImplementedException();
        }

        public override void RewritePath(HttpContext context)
        {
            throw new System.NotImplementedException();
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
            return creationDate.AddDays(this.MaxAge) < DateTime.UtcNow.AddDays(-this.MaxAge);
        }
    }
}
