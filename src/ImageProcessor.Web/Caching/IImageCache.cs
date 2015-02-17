
namespace ImageProcessor.Web.Caching
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;

    public interface IImageCache
    {
        /// <summary>
        /// Gets or sets any additional settings required by the cache.
        /// </summary>
        Dictionary<string, string> Settings { get; }

        string CachedPath { get; }

        int MaxAge { get; }

        Task<bool> IsNewOrUpdatedAsync();

        Task AddImageToCacheAsync(Stream stream);

        Task TrimCacheAsync();

        Task<string> CreateCachedFileName();

        void RewritePath(HttpContext context);

        //void SetHeaders(HttpContext context);
    }
}
