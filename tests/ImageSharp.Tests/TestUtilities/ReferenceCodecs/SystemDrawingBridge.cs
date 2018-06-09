// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Drawing.Imaging;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public static class SystemDrawingBridge
    {
        internal static unsafe Image<TPixel> FromFromArgb32SystemDrawingBitmap<TPixel>(System.Drawing.Bitmap bmp)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = bmp.Width;
            int h = bmp.Height;

            var fullRect = new System.Drawing.Rectangle(0, 0, w, h);

            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ArgumentException($"FromFromArgb32SystemDrawingBitmap(): pixel format should be Argb32!", nameof(bmp));
            }

            BitmapData data = bmp.LockBits(fullRect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte* sourcePtrBase = (byte*)data.Scan0;

            long sourceRowByteCount = data.Stride;
            long destRowByteCount = w * sizeof(Argb32);

            var image = new Image<TPixel>(w, h);

            using (IBuffer<Bgra32> workBuffer = Configuration.Default.MemoryManager.Allocate<Bgra32>(w))
            {
                fixed (Bgra32* destPtr = &workBuffer.GetReference())
                {
                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);

                        byte* sourcePtr = sourcePtrBase + data.Stride * y;

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                        PixelOperations<TPixel>.Instance.PackFromBgra32(workBuffer.GetSpan(), row, row.Length);
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// TODO: Doesn not work yet!
        /// </summary>
        internal static unsafe Image<TPixel> FromFromRgb24SystemDrawingBitmap<TPixel>(System.Drawing.Bitmap bmp)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = bmp.Width;
            int h = bmp.Height;

            var fullRect = new System.Drawing.Rectangle(0, 0, w, h);

            if (bmp.PixelFormat != PixelFormat.Format24bppRgb)
            {
                throw new ArgumentException($"FromFromArgb32SystemDrawingBitmap(): pixel format should be Rgb24!", nameof(bmp));
            }

            BitmapData data = bmp.LockBits(fullRect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte* sourcePtrBase = (byte*)data.Scan0;

            long sourceRowByteCount = data.Stride;
            long destRowByteCount = w * sizeof(Bgr24);

            var image = new Image<TPixel>(w, h);

            using (IBuffer<Bgr24> workBuffer = Configuration.Default.MemoryManager.Allocate<Bgr24>(w))
            {
                fixed (Bgr24* destPtr = &workBuffer.GetReference())
                {
                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);

                        byte* sourcePtr = sourcePtrBase + data.Stride * y;

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                        PixelOperations<TPixel>.Instance.PackFromBgr24(workBuffer.GetSpan(), row, row.Length);

                        // FromRgb24(workBuffer.GetSpan(), row);
                    }
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

            long destRowByteCount = data.Stride;
            long sourceRowByteCount = w * sizeof(Bgra32);

            using (IBuffer<Bgra32> workBuffer = image.GetConfiguration().MemoryManager.Allocate<Bgra32>(w))
            {
                fixed (Bgra32* sourcePtr = &workBuffer.GetReference())
                {

                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);
                        PixelOperations<TPixel>.Instance.ToBgra32(row, workBuffer.GetSpan(), row.Length);
                        byte* destPtr = destPtrBase + data.Stride * y;

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                    }
                }
            }

            resultBitmap.UnlockBits(data);

            return resultBitmap;
        }
    }
}