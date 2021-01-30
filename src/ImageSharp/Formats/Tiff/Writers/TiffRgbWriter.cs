// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
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
    internal class TiffRgbWriter : TiffBaseColorWriter
    {
        public TiffRgbWriter(TiffStreamWriter output, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
            : base(output, memoryAllocator, configuration, entriesCollector)
        {
        }

        /// <summary>
        /// Writes the image data as RGB to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantizer">The quantizer.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate compression.</param>
        /// <returns>
        /// The number of bytes written.
        /// </returns>
        public override int Write<TPixel>(Image<TPixel> image, IQuantizer quantizer, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
        {
            using IManagedByteBuffer row = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width * 3);
            Span<byte> rowSpan = row.GetSpan();
            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteDeflateCompressedRgb(image, rowSpan, compressionLevel, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.Lzw)
            {
                return this.WriteLzwCompressedRgb(image, rowSpan, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteRgbPackBitsCompressed(image, rowSpan);
            }

            // No compression.
            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);
                this.Output.Write(rowSpan);
                bytesWritten += rowSpan.Length;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as RGB compressed with zlib to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteDeflateCompressedRgb<TPixel>(Image<TPixel> image, Span<byte> rowSpan, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.MemoryAllocator, memoryStream, compressionLevel);

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction24Bit(rowSpan);
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
        /// Writes the image data as RGB compressed with lzw to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteLzwCompressedRgb<TPixel>(Image<TPixel> image, Span<byte> rowSpan, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();

            IMemoryOwner<byte> pixelData = this.MemoryAllocator.Allocate<byte>(image.Width * image.Height * 3);
            Span<byte> pixels = pixelData.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction24Bit(rowSpan);
                }

                rowSpan.CopyTo(pixels.Slice(y * image.Width * 3));
            }

            using var lzwEncoder = new TiffLzwEncoder(this.MemoryAllocator, pixelData);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.Output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as RGB with packed bits compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteRgbPackBitsCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = ((image.Width * 3) / 127) + 1;
            using IManagedByteBuffer compressedRow = this.MemoryAllocator.AllocateManagedByteBuffer((image.Width * 3) + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();
            int bytesWritten = 0;

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.Configuration, pixelRow, rowSpan, pixelRow.Length);
                int size = PackBitsWriter.PackBits(rowSpan, compressedRowSpan);
                this.Output.Write(compressedRow.Slice(0, size));
                bytesWritten += size;
                compressedRowSpan.Clear();
            }

            return bytesWritten;
        }
    }
}
