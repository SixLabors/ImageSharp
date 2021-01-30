// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffBiColorWriter : TiffWriter
    {
        public TiffBiColorWriter(Stream output, MemoryAllocator memoryAllocator, Configuration configuration)
            : base(output, memoryAllocator, configuration)
        {
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColor<TPixel>(Image<TPixel> image, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = image.Width % 8 == 0 ? 0 : 1;
            int bytesPerRow = (image.Width / 8) + padding;
            using IMemoryOwner<L8> pixelRowAsGray = this.MemoryAllocator.Allocate<L8>(image.Width);
            using IManagedByteBuffer row = this.MemoryAllocator.AllocateManagedByteBuffer(bytesPerRow, AllocationOptions.Clean);
            Span<byte> outputRow = row.GetSpan();
            Span<L8> pixelRowAsGraySpan = pixelRowAsGray.GetSpan();

            // Convert image to black and white.
            // TODO: Should we allow to skip this by the user, if its known to be black and white already?
            using Image<TPixel> imageBlackWhite = image.Clone();
            imageBlackWhite.Mutate(img => img.BinaryDither(default(ErrorDither)));

            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteBiColorDeflate(imageBlackWhite, pixelRowAsGraySpan, outputRow, compressionLevel);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteBiColorPackBits(imageBlackWhite, pixelRowAsGraySpan, outputRow);
            }

            if (compression == TiffEncoderCompression.CcittGroup3Fax)
            {
                var bitWriter = new T4BitWriter(this.MemoryAllocator, this.Configuration);
                return bitWriter.CompressImage(imageBlackWhite, pixelRowAsGraySpan, this.Output);
            }

            if (compression == TiffEncoderCompression.ModifiedHuffman)
            {
                var bitWriter = new T4BitWriter(this.MemoryAllocator, this.Configuration, useModifiedHuffman: true);
                return bitWriter.CompressImage(imageBlackWhite, pixelRowAsGraySpan, this.Output);
            }

            // Write image uncompressed.
            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = imageBlackWhite.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.Configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                this.Output.Write(row);
                bytesWritten += row.Length();

                row.Clear();
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white with deflate compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="pixelRowAsGraySpan">A span for converting a pixel row to gray.</param>
        /// <param name="outputRow">A span which will be used to store the output pixels.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColorDeflate<TPixel>(Image<TPixel> image, Span<L8> pixelRowAsGraySpan, Span<byte> outputRow, DeflateCompressionLevel compressionLevel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.MemoryAllocator, memoryStream, compressionLevel);

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.Configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                deflateStream.Write(outputRow);

                outputRow.Clear();
            }

            deflateStream.Flush();
            byte[] buffer = memoryStream.ToArray();
            this.Output.Write(buffer);
            bytesWritten += buffer.Length;

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white with pack bits compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="pixelRowAsGraySpan">A span for converting a pixel row to gray.</param>
        /// <param name="outputRow">A span which will be used to store the output pixels.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColorPackBits<TPixel>(Image<TPixel> image, Span<L8> pixelRowAsGraySpan, Span<byte> outputRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bits.
            int additionalBytes = (image.Width / 127) + 2;
            int compressedRowBytes = (image.Width / 8) + additionalBytes;
            using IManagedByteBuffer compressedRow = this.MemoryAllocator.AllocateManagedByteBuffer(compressedRowBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.Configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                var size = PackBitsWriter.PackBits(outputRow, compressedRowSpan);
                this.Output.Write(compressedRowSpan.Slice(0, size));
                bytesWritten += size;

                outputRow.Clear();
            }

            return bytesWritten;
        }
    }
}
