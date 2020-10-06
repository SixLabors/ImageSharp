using SixLabors.ImageSharp.Formats.WebP;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="ImageMetadata"/> type.
    /// </summary>
    public static partial class MetadataExtensions
    {
        /// <summary>
        /// Gets the webp format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="WebPMetadata"/>.</returns>
        public static WebPMetadata GetWebpMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(WebPFormat.Instance);
    }
}
