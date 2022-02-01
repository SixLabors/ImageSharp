// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Methods for encoding the alpha data of a VP8 image.
    /// </summary>
    internal static class AlphaEncoder
    {
        /// <summary>
        /// Encodes the alpha channel data.
        /// Data is either compressed as lossless webp image or uncompressed.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        /// <param name="compress">Indicates, if the data should be compressed with the lossless webp compression.</param>
        /// <returns>The alpha data.</returns>
        public static byte[] EncodeAlpha<TPixel>(Image<TPixel> image, Configuration configuration, MemoryAllocator memoryAllocator, bool compress)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            byte[] alphaData = ExtractAlphaChannel(image, configuration, memoryAllocator);
            int width = image.Width;
            int height = image.Height;
            if (compress)
            {
                WebpEncodingMethod effort = WebpEncodingMethod.Default;
                int quality = 8 * (int)effort;
                using var lossLessEncoder = new Vp8LEncoder(
                    memoryAllocator,
                    configuration,
                    width,
                    height,
                    quality,
                    effort,
                    WebpTransparentColorMode.Preserve,
                    false,
                    0);

                // The transparency information will be stored in the green channel of the ARGB quadruplet.
                // The green channel is allowed extra transformation steps in the specification -- unlike the other channels,
                // that can improve compression.
                using Image<Rgba32> alphaAsImage = DispatchAlphaToGreen(image, alphaData);

                return lossLessEncoder.EncodeAlphaImageData(alphaAsImage);
            }

            return alphaData;
        }

        /// <summary>
        /// Store the transparency in the green channel.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="alphaData">A byte sequence of length width * height, containing all the 8-bit transparency values in scan order.</param>
        /// <returns>The transparency image.</returns>
        private static Image<Rgba32> DispatchAlphaToGreen<TPixel>(Image<TPixel> image, byte[] alphaData)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;
            var alphaAsImage = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                Memory<Rgba32> rowBuffer = alphaAsImage.DangerousGetPixelRowMemory(y);
                Span<Rgba32> pixelRow = rowBuffer.Span;
                Span<byte> alphaRow = alphaData.AsSpan(y * width, width);
                for (int x = 0; x < width; x++)
                {
                    // Leave A/R/B channels zero'd.
                    pixelRow[x] = new Rgba32(0, alphaRow[x], 0, 0);
                }
            }

            return alphaAsImage;
        }

        /// <summary>
        /// Extract the alpha data of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        /// <returns>A byte sequence of length width * height, containing all the 8-bit transparency values in scan order.</returns>
        private static byte[] ExtractAlphaChannel<TPixel>(Image<TPixel> image, Configuration configuration, MemoryAllocator memoryAllocator)
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
