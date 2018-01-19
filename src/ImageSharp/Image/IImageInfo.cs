using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents raw image information.
    /// </summary>
    public interface IImageInfo
    {
        /// <summary>
        /// Gets the raw image pixel type information.
        /// </summary>
        PixelTypeInfo PixelType { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the meta data of the image.
        /// </summary>
        ImageMetaData MetaData { get; }
    }
}