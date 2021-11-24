// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Pixel encoding methods for the PBM plain encoding.
    /// </summary>
    internal class PlainEncoder
    {
        private const int MaxLineLength = 70;
        private const byte NewLine = 0x0a;
        private const byte Space = 0x20;
        private const byte Zero = 0x30;
        private const byte One = 0x31;

        /// <summary>
        /// Decode pixels into the PBM plain encoding.
        /// </summary>
        /// <typeparam name="TPixel">The type of input pixel.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The bytestream to write to.</param>
        /// <param name="image">The input image.</param>
        /// <param name="colorType">The ColorType to use.</param>
        /// <param name="maxPixelValue">The maximum expected pixel value</param>
        public static void WritePixels<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image, PbmColorType colorType, int maxPixelValue)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (colorType == PbmColorType.Grayscale)
            {
                if (maxPixelValue < 256)
                {
                    WriteGrayscale(configuration, stream, image);
                }
                else
                {
                    WriteWideGrayscale(configuration, stream, image);
                }
            }
            else if (colorType == PbmColorType.Rgb)
            {
                if (maxPixelValue < 256)
                {
                    WriteRgb(configuration, stream, image);
                }
                else
                {
                    WriteWideRgb(configuration, stream, image);
                }
            }
            else
            {
                WriteBlackAndWhite(configuration, stream, image);
            }
        }

        private static void WriteGrayscale<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Size().Width;
            int height = image.Size().Height;
            int bytesWritten = -1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
            Span<L8> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelSpan = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(
                    configuration,
                    pixelSpan,
                    rowSpan);

                for (int x = 0; x < width; x++)
                {
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].PackedValue);
                }
            }
        }

        private static void WriteWideGrayscale<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Size().Width;
            int height = image.Size().Height;
            int bytesWritten = -1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<L16> row = allocator.Allocate<L16>(width);
            Span<L16> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelSpan = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL16(
                    configuration,
                    pixelSpan,
                    rowSpan);

                for (int x = 0; x < width; x++)
                {
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].PackedValue);
                }
            }
        }

        private static void WriteRgb<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Size().Width;
            int height = image.Size().Height;
            int bytesWritten = -1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<Rgb24> row = allocator.Allocate<Rgb24>(width);
            Span<Rgb24> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelSpan = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24(
                    configuration,
                    pixelSpan,
                    rowSpan);

                for (int x = 0; x < width; x++)
                {
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].R);
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].G);
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].B);
                }
            }
        }

        private static void WriteWideRgb<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Size().Width;
            int height = image.Size().Height;
            int bytesWritten = -1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<Rgb48> row = allocator.Allocate<Rgb48>(width);
            Span<Rgb48> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelSpan = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb48(
                    configuration,
                    pixelSpan,
                    rowSpan);

                for (int x = 0; x < width; x++)
                {
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].R);
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].G);
                    WriteWhitespace(stream, ref bytesWritten);
                    bytesWritten += stream.WriteDecimal(rowSpan[x].B);
                }
            }
        }

        private static void WriteBlackAndWhite<TPixel>(Configuration configuration, Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Size().Width;
            int height = image.Size().Height;
            int bytesWritten = -1;
            MemoryAllocator allocator = configuration.MemoryAllocator;
            using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
            Span<L8> rowSpan = row.GetSpan();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelSpan = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(
                    configuration,
                    pixelSpan,
                    rowSpan);

                for (int x = 0; x < width; x++)
                {
                    WriteWhitespace(stream, ref bytesWritten);
                    if (rowSpan[x].PackedValue > 127)
                    {
                        stream.WriteByte(Zero);
                    }
                    else
                    {
                        stream.WriteByte(One);
                    }

                    bytesWritten++;
                }
            }
        }

        private static void WriteWhitespace(Stream stream, ref int bytesWritten)
        {
            if (bytesWritten > MaxLineLength)
            {
                stream.WriteByte(NewLine);
                bytesWritten = 1;
            }
            else if (bytesWritten == -1)
            {
                bytesWritten = 0;
            }
            else
            {
                stream.WriteByte(Space);
                bytesWritten++;
            }
        }
    }
}
