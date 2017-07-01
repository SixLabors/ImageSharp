namespace ImageSharp.Tests.TestUtilities.Integration
{
    using System;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class ReferencePngDecoder : IImageDecoder
    {
        public static ReferencePngDecoder Instance { get; } = new ReferencePngDecoder();

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var sdBitmap = new System.Drawing.Bitmap(stream))
            {
                if (!sdBitmap.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Png))
                {
                    throw new Exception("Reference image should be a Png!");
                }
                if (sdBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    throw new Exception("Reference image pixel format should be PixelFormat.Format32bppArgb!");
                }

                return IntegrationTestUtils.FromSystemDrawingBitmap<TPixel>(sdBitmap);
            }
        }
    }
}