using System;
using System.Collections.Generic;
using System.Text;

namespace ImageSharp.Tests.TestUtilities.Integration
{
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class ReferenceEncoder : IImageEncoder
    {
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, IEncoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            System.Drawing.Bitmap sdBitmap = IntegrationTestUtils.ToSystemDrawingBitmap(image);
            throw new NotImplementedException();
        }
    }
}
