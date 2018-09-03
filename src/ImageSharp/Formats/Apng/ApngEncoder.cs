using System;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Apng
{
    public class ApngEncoder : IImageEncoder
    {
        public void Encode<TPixel>(Image<TPixel> image, Stream stream) where TPixel : struct, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }
    }

    // ref: https://wiki.mozilla.org/APNG_Specification

}
