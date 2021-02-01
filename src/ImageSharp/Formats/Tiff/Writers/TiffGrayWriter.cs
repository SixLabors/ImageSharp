// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffGrayWriter : TiffBaseColorWriter
    {
        public TiffGrayWriter(TiffStreamWriter output, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
            : base(output, memoryAllocator, configuration, entriesCollector)
        {
        }

        /// <summary>
        /// Writes the image data as 8 bit gray to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantizer">The quantizer.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate or lzw compression.</param>
        /// <returns>
        /// The number of bytes written.
        /// </returns>
        public override int Write<TPixel>(Image<TPixel> image, IQuantizer quantizer, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
        {
            using IManagedByteBuffer row = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width);
            Span<byte> rowSpan = row.GetSpan();

            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteGrayDeflateCompressed(image, rowSpan, compressionLevel, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.Lzw)
            {
                return this.WriteGrayLzwCompressed(image, rowSpan, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteGrayPackBitsCompressed(image, rowSpan);
            }

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);
                this.Output.Write(rowSpan);
                bytesWritten += rowSpan.Length;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray with deflate compression to the stream.
        /// </summary>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayDeflateCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.MemoryAllocator, memoryStream, compressionLevel);

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(rowSpan);
                }

                deflateStream.Write(rowSpan);
            }

            deflateStream.Flush();

            byte[] buffer = memoryStream.ToArray();
            this.Output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray with lzw compression to the stream.
        /// </summary>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayLzwCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();

            IMemoryOwner<byte> pixelData = this.MemoryAllocator.Allocate<byte>(image.Width * image.Height);
            Span<byte> pixels = pixelData.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);
                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(rowSpan);
                }

                rowSpan.CopyTo(pixels.Slice(y * image.Width));
            }

            using var lzwEncoder = new TiffLzwEncoder(this.MemoryAllocator, pixelData);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.Output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayPackBitsCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = (image.Width / 127) + 1;
            using IManagedByteBuffer compressedRow = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);
                int size = PackBitsWriter.PackBits(rowSpan, compressedRowSpan);
                this.Output.Write(compressedRow.Slice(0, size));
                bytesWritten += size;
                compressedRowSpan.Clear();
            }

            return bytesWritten;
        }
    }
}
