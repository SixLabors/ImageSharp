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

        private static void FromArgb32<TPixel>(Span<Argb32> source, Span<TPixel> dest)
            where TPixel : struct, IPixel<TPixel>
        {
            int length = source.Length;
            Guard.MustBeSizedAtLeast(dest, length, nameof(dest));

            using (var rgbaBuffer = new Buffer<Rgba32>(length))
            {
                PixelOperations<Argb32>.Instance.ToRgba32(source, rgbaBuffer, length);

                for (int i = 0; i < length; i++)
                {
                    ref Rgba32 s = ref rgbaBuffer[i];
                    ref TPixel d = ref dest[i];

                    d.PackFromRgba32(s);
                }
            }
        }

        internal static unsafe Image<TPixel> FromSystemDrawingBitmap<TPixel>(System.Drawing.Bitmap bmp)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = bmp.Width;
            int h = bmp.Height;

            var fullRect = new System.Drawing.Rectangle(0, 0, w, h);

            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ArgumentException("FromSystemDrawingBitmap(): pixel format not supported", nameof(bmp));
            }

            BitmapData data = bmp.LockBits(fullRect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte* sourcePtrBase = (byte*)data.Scan0;

            long sourceRowByteCount = data.Stride;
            long destRowByteCount = w * sizeof(Argb32);

            var image = new Image<TPixel>(w, h);

            using (var workBuffer = new Buffer<Argb32>(w))
            {
                var destPtr = (Argb32*)workBuffer.Pin();
                for (int y = 0; y < h; y++)
                {
                    Span<TPixel> row = image.GetRowSpan(y);
                    
                    byte* sourcePtr = sourcePtrBase + data.Stride * y;

                    Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);

                    FromArgb32(workBuffer, row);
                }
            }

            return image;
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