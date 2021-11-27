// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Pixel decoding methods for the PBM binary encoding.
    /// </summary>
    internal class BinaryDecoder
    {
        /// <summary>
        /// Decode the specified pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel to encode to.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixels">The pixel array to encode into.</param>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="colorType">The ColorType to decode.</param>
        /// <param name="maxPixelValue">The maximum expected pixel value</param>
        /// <exception cref="InvalidImageContentException">
        /// Thrown if an invalid combination of setting is requested.
        /// </exception>
        public static void Process<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream, PbmColorType colorType, int maxPixelValue)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (colorType == PbmColorType.Grayscale)
            {
                if (maxPixelValue < 256)
                {
                    ProcessGrayscale(configuration, pixels, stream);
                }
                else
                {
                    ProcessWideGrayscale(configuration, pixels, stream);
                }
            }
            else if (colorType == PbmColorType.Rgb)
            {
                if (maxPixelValue < 256)
                {
                    ProcessRgb(configuration, pixels, stream);
                }
                else
                {
                    ProcessWideRgb(configuration, pixels, stream);
                }
            }
            else
            {
                ProcessBlackAndWhite(configuration, pixels, stream);
            }
        }

        private static void ProcessGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            int bytesPerPixel = 1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                stream.Read(rowSpan);
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromL8Bytes(
                    configuration,
                    rowSpan,
                    pixelSpan,
                    width);
            }
        }

        private static void ProcessWideGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            int bytesPerPixel = 2;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                stream.Read(rowSpan);
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromL16Bytes(
                    configuration,
                    rowSpan,
                    pixelSpan,
                    width);
            }
        }

        private static void ProcessRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            int bytesPerPixel = 3;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                stream.Read(rowSpan);
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(
                    configuration,
                    rowSpan,
                    pixelSpan,
                    width);
            }
        }

        private static void ProcessWideRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            int bytesPerPixel = 6;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                stream.Read(rowSpan);
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromRgb48Bytes(
                    configuration,
                    rowSpan,
                    pixelSpan,
                    width);
            }
        }

        private static void ProcessBlackAndWhite<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            int startBit = 0;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
            Span<L8> rowSpan = row.GetSpan();
            var white = new L8(255);
            var black = new L8(0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width;)
                {
                    int raw = stream.ReadByte();
                    int bit = startBit;
                    startBit = 0;
                    for (; bit < 8; bit++)
                    {
                        bool bitValue = (raw & (0x80 >> bit)) != 0;
                        rowSpan[x] = bitValue ? black : white;
                        x++;
                        if (x == width)
                        {
                            startBit = (bit + 1) & 7; // Round off to below 8.
                            if (startBit != 0)
                            {
                                stream.Seek(-1, System.IO.SeekOrigin.Current);
                            }

                            break;
                        }
                    }
                }

                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromL8(
                    configuration,
                    rowSpan,
                    pixelSpan);
            }
        }
    }
}
