using System;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Apng
{
    public class ApngDecoder : IImageDecoder
    {
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream) where TPixel : struct, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }
    }

    // ref: https://wiki.mozilla.org/APNG_Specification

}
