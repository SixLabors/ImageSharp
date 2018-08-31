// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    /// <summary>
    /// Provides methods to convert to/from System.Drawing bitmaps.
    /// </summary>
    public static class SystemDrawingBridge
    {
        /// <summary>
        /// Returns an image from the given System.Drawing bitmap.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="bmp">The input bitmap.</param>
        /// <exception cref="ArgumentException">Thrown if the image pixel format is not of type <see cref="PixelFormat.Format32bppArgb"/></exception>
        internal static unsafe Image<TPixel> From32bppArgbSystemDrawingBitmap<TPixel>(Bitmap bmp)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = bmp.Width;
            int h = bmp.Height;

            var fullRect = new Rectangle(0, 0, w, h);

            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ArgumentException($"{nameof(From32bppArgbSystemDrawingBitmap)} : pixel format should be {PixelFormat.Format32bppArgb}!", nameof(bmp));
            }

            BitmapData data = bmp.LockBits(fullRect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte* sourcePtrBase = (byte*)data.Scan0;

            long sourceRowByteCount = data.Stride;
            long destRowByteCount = w * sizeof(Bgra32);

            var image = new Image<TPixel>(w, h);

            using (IMemoryOwner<Bgra32> workBuffer = Configuration.Default.MemoryAllocator.Allocate<Bgra32>(w))
            {
                fixed (Bgra32* destPtr = &workBuffer.GetReference())
                {
                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);

                        byte* sourcePtr = sourcePtrBase + (data.Stride * y);

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                        PixelOperations<TPixel>.Instance.PackFromBgra32(workBuffer.GetSpan(), row, row.Length);
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// Returns an image from the given System.Drawing bitmap.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="bmp">The input bitmap.</param>
        /// <exception cref="ArgumentException">Thrown if the image pixel format is not of type <see cref="PixelFormat.Format24bppRgb"/></exception>
        internal static unsafe Image<TPixel> From24bppRgbSystemDrawingBitmap<TPixel>(Bitmap bmp)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = bmp.Width;
            int h = bmp.Height;

            var fullRect = new Rectangle(0, 0, w, h);

            if (bmp.PixelFormat != PixelFormat.Format24bppRgb)
            {
                throw new ArgumentException($"{nameof(From24bppRgbSystemDrawingBitmap)}: pixel format should be {PixelFormat.Format24bppRgb}!", nameof(bmp));
            }

            BitmapData data = bmp.LockBits(fullRect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte* sourcePtrBase = (byte*)data.Scan0;

            long sourceRowByteCount = data.Stride;
            long destRowByteCount = w * sizeof(Bgr24);

            var image = new Image<TPixel>(w, h);

            using (IMemoryOwner<Bgr24> workBuffer = Configuration.Default.MemoryAllocator.Allocate<Bgr24>(w))
            {
                fixed (Bgr24* destPtr = &workBuffer.GetReference())
                {
                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);

                        byte* sourcePtr = sourcePtrBase + (data.Stride * y);

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                        PixelOperations<TPixel>.Instance.PackFromBgr24(workBuffer.GetSpan(), row, row.Length);
                    }
                }
            }

            return image;
        }

        internal static unsafe Bitmap To32bppArgbSystemDrawingBitmap<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            int w = image.Width;
            int h = image.Height;

            var resultBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var fullRect = new Rectangle(0, 0, w, h);
            BitmapData data = resultBitmap.LockBits(fullRect, ImageLockMode.ReadWrite, resultBitmap.PixelFormat);
            byte* destPtrBase = (byte*)data.Scan0;

            long destRowByteCount = data.Stride;
            long sourceRowByteCount = w * sizeof(Bgra32);

            using (IMemoryOwner<Bgra32> workBuffer = image.GetConfiguration().MemoryAllocator.Allocate<Bgra32>(w))
            {
                fixed (Bgra32* sourcePtr = &workBuffer.GetReference())
                {
                    for (int y = 0; y < h; y++)
                    {
                        Span<TPixel> row = image.Frames.RootFrame.GetPixelRowSpan(y);
                        PixelOperations<TPixel>.Instance.ToBgra32(row, workBuffer.GetSpan(), row.Length);
                        byte* destPtr = destPtrBase + (data.Stride * y);

                        Buffer.MemoryCopy(sourcePtr, destPtr, destRowByteCount, sourceRowByteCount);
                    }
                }
            }

            resultBitmap.UnlockBits(data);

            return resultBitmap;
        }
    }
}