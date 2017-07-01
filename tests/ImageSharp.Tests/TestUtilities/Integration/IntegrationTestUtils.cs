namespace ImageSharp.Tests.TestUtilities.Integration
{
    using System;
    using System.Drawing.Imaging;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    public static class IntegrationTestUtils
    {
        // TODO: It would be nice to have this method in PixelOperations<T>
        private static void ToArgb32<TPixel>(Span<TPixel> source, Span<Argb32> dest)
            where TPixel : struct, IPixel<TPixel>
        {
            int length = source.Length;
            Guard.MustBeSizedAtLeast(dest, length, nameof(dest));
            
            using (var rgbaBuffer = new Buffer<Rgba32>(length))
            {
                PixelOperations<TPixel>.Instance.ToRgba32(source, rgbaBuffer, length);

                for (int i = 0; i < length; i++)
                {
                    ref Rgba32 s = ref rgbaBuffer[i];
                    ref Argb32 d = ref dest[i];

                    d.PackFromRgba32(s);
                }
            }
        }

        internal static unsafe System.Drawing.Bitmap ToSystemDrawingBitmap<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = image.Width;
            int h = image.Height;
            
            var resultBitmap = new System.Drawing.Bitmap(w, h, PixelFormat.Format32bppArgb);
            var fullRect = new System.Drawing.Rectangle(0, 0, w, h);
            BitmapData data = resultBitmap.LockBits(fullRect, ImageLockMode.ReadWrite, resultBitmap.PixelFormat);
            byte* destPtrBase = (byte*)data.Scan0;

            long destRowByteCount= data.Stride;
            long sourceRowByteCount = w * sizeof(Argb32);

            using (var workBuffer = new Buffer<Argb32>(w))
            {
                var sourcePtr = (Argb32*) workBuffer.Pin();
                
                for (int y = 0; y < h; y++)
                {
                    Span<TPixel> row = image.GetRowSpan(y);
                    ToArgb32(row, workBuffer);
                    byte* destPtr = destPtrBase + data.Stride * y;

                    Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                }
            }
                
            resultBitmap.UnlockBits(data);

            return resultBitmap;
        }
    }
}