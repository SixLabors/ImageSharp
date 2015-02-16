
namespace ImageProcessor.Web.Caching
{
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;

    public interface IImageCache
    {
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
