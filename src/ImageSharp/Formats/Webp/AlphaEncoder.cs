// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Encodes the alpha channel data.
    /// Data is either compressed as lossless webp image or uncompressed.
    /// </summary>
    internal static class AlphaEncoder
    {
        public static byte[] EncodeAlpha<TPixel>(Image<TPixel> image, Configuration configuration, MemoryAllocator memoryAllocator)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> imageBuffer = image.Frames.RootFrame.PixelBuffer;
            int height = image.Height;
            int width = image.Width;
            byte[] alphaData = new byte[width * height];

            using IMemoryOwner<Rgba32> rowBuffer = memoryAllocator.Allocate<Rgba32>(width);
            Span<Rgba32> rgbaRow = rowBuffer.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> rowSpan = imageBuffer.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgba32(configuration, rowSpan, rgbaRow);
                int offset = y * width;
                for (int x = 0; x < width; x++)
                {
                    alphaData[offset + x] = rgbaRow[x].A;
                }
            }

            return alphaData;
        }
    }
}
