using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp
{
    internal sealed class ImageInfo : IImage
    {
        public ImageInfo(PixelTypeInfo pixelType, int width, int height, ImageMetaData metaData)
        {
            this.PixelType = pixelType;
            this.Width = width;
            this.Height = height;
            this.MetaData = metaData;
        }

        public PixelTypeInfo PixelType { get; }

        public int Width { get; }

        public int Height { get; }

        public ImageMetaData MetaData { get; }
    }
}