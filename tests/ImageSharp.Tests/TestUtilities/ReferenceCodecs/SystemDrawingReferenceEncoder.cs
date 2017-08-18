using System.Collections.Generic;
using System.Text;

namespace ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    using System.Drawing.Imaging;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class SystemDrawingReferenceEncoder : IImageEncoder
    {
        private readonly System.Drawing.Imaging.ImageFormat imageFormat;

        public SystemDrawingReferenceEncoder(ImageFormat imageFormat)
        {
            this.imageFormat = imageFormat;
        }

        public static SystemDrawingReferenceEncoder Png { get; } = new SystemDrawingReferenceEncoder(System.Drawing.Imaging.ImageFormat.Png);

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.ToSystemDrawingBitmap(image))
            {
                sdBitmap.Save(stream, this.imageFormat);
            }
        }
    }
}
