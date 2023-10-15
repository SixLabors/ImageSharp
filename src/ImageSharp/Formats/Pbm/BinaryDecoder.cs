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
        private static L8 white = new(255);
        private static L8 black = new(0);

        /// <summary>
        /// Decode the specified pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel to encode to.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixels">The pixel array to encode into.</param>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="colorType">The ColorType to decode.</param>
        /// <param name="componentType">Data type of the pixles components.</param>
        /// <exception cref="InvalidImageContentException">
        /// Thrown if an invalid combination of setting is requested.
        /// </exception>
        public static void Process<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream, PbmColorType colorType, PbmComponentType componentType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (colorType == PbmColorType.Grayscale)
            {
                if (componentType == PbmComponentType.Byte)
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
                if (componentType == PbmComponentType.Byte)
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
            const int bytesPerPixel = 1;
            int width = pixels.Width;
            int height = pixels.Height;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                if (stream.Read(rowSpan) < rowSpan.Length)
                {
                    return;
                }

                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
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
            const int bytesPerPixel = 2;
            int width = pixels.Width;
            int height = pixels.Height;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                if (stream.Read(rowSpan) < rowSpan.Length)
                {
                    return;
                }

                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
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
            const int bytesPerPixel = 3;
            int width = pixels.Width;
            int height = pixels.Height;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                if (stream.Read(rowSpan) < rowSpan.Length)
                {
                    return;
                }

                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
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
            const int bytesPerPixel = 6;
            int width = pixels.Width;
            int height = pixels.Height;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                if (stream.Read(rowSpan) < rowSpan.Length)
                {
                    return;
                }

                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
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
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
            Span<L8> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width;)
                {
                    int raw = stream.ReadByte();
                    if (raw < 0)
                    {
                        return;
                    }

                    int stopBit = Math.Min(8, width - x);
                    for (int bit = 0; bit < stopBit; bit++)
                    {
                        bool bitValue = (raw & (0x80 >> bit)) != 0;
                        rowSpan[x] = bitValue ? black : white;
                        x++;
                    }
                }

                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromL8(
                    configuration,
                    rowSpan,
                    pixelSpan);
            }
        }
    }
}
