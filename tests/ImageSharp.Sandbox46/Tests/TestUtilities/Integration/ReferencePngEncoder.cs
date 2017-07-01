using System;
using System.Collections.Generic;
using System.Text;

namespace ImageSharp.Tests.TestUtilities.Integration
{
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class ReferencePngEncoder : IImageEncoder
    {
        public static ReferencePngEncoder Instance { get; } = new ReferencePngEncoder();

        public void Encode<TPixel>(Image<TPixel> image, Stream stream, IEncoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            using (System.Drawing.Bitmap sdBitmap = IntegrationTestUtils.ToSystemDrawingBitmap(image))
            {
                sdBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
