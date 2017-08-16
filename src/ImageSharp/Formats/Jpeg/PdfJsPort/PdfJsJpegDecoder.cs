namespace ImageSharp.Formats.Jpeg.PdfJsPort
{
    using System;
    using System.IO;

    using ImageSharp.PixelFormats;

    internal sealed class PdfJsJpegDecoder : IImageDecoder, IJpegDecoderOptions
    {
        public bool IgnoreMetadata { get; set; }

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var decoder = new PdfJsJpegDecoderCore(configuration, this))
            {
                return decoder.Decode<TPixel>(stream);
            }
        }
    }
}