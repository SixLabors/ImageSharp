namespace ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    using System;
    using System.Drawing;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    public class ReferenceDecoder : IImageDecoder
    {
        public static ReferenceDecoder Instance { get; } = new ReferenceDecoder();

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var sourceBitmap = new System.Drawing.Bitmap(stream))
            {
                if (sourceBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    return SystemDrawingBridge.FromSystemDrawingBitmap<TPixel>(sourceBitmap);
                }

                using (var convertedBitmap = new System.Drawing.Bitmap(
                    sourceBitmap.Width,
                    sourceBitmap.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(convertedBitmap))
                    {
                        g.DrawImage(sourceBitmap, new PointF(0, 0));
                    }
                    return SystemDrawingBridge.FromSystemDrawingBitmap<TPixel>(convertedBitmap);
                }
            }
        }
    }
}