// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffWriter : IDisposable
    {
        private readonly Stream output;

        private readonly MemoryAllocator memoryAllocator;

        private readonly Configuration configuration;

        private readonly byte[] paddingBytes = new byte[4];

        private readonly List<long> references = new List<long>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffWriter"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="memoryMemoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        public TiffWriter(Stream output, MemoryAllocator memoryMemoryAllocator, Configuration configuration)
        {
            this.output = output;
            this.memoryAllocator = memoryMemoryAllocator;
            this.configuration = configuration;
        }

        /// <summary>
        /// Gets a value indicating whether the architecture is little-endian.
        /// </summary>
        public bool IsLittleEndian => BitConverter.IsLittleEndian;

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        public long Position => this.output.Position;

        /// <summary>
        /// Writes an empty four bytes to the stream, returning the offset to be written later.
        /// </summary>
        /// <returns>The offset to be written later</returns>
        public long PlaceMarker()
        {
            long offset = this.output.Position;
            this.Write(0u);
            return offset;
        }

        /// <summary>
        /// Writes an array of bytes to the current stream.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void Write(byte[] value)
        {
            this.output.Write(value, 0, value.Length);
        }

        /// <summary>
        /// Writes a byte to the current stream.
        /// </summary>
        /// <param name="value">The byte to write.</param>
        public void Write(byte value)
        {
            this.output.Write(new byte[] { value }, 0, 1);
        }

        /// <summary>
        /// Writes a two-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The two-byte unsigned integer to write.</param>
        public void Write(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.output.Write(bytes, 0, 2);
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void Write(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.output.Write(bytes, 0, 4);
        }

        /// <summary>
        /// Writes an array of bytes to the current stream, padded to four-bytes.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void WritePadded(byte[] value)
        {
            this.output.Write(value, 0, value.Length);

            if (value.Length < 4)
            {
                this.output.Write(this.paddingBytes, 0, 4 - value.Length);
            }
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the specified marker in the stream.
        /// </summary>
        /// <param name="offset">The offset returned when placing the marker</param>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void WriteMarker(long offset, uint value)
        {
            long currentOffset = this.output.Position;
            this.output.Seek(offset, SeekOrigin.Begin);
            this.Write(value);
            this.output.Seek(currentOffset, SeekOrigin.Begin);
        }

        /// <summary>
        /// Writes the image data as RGB to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="padding">The padding bytes for each row.</param>
        /// <returns>The number of bytes written</returns>
        public int WriteRgbImageData<TPixel>(Image<TPixel> image, int padding)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer row = this.AllocateRow(image.Width, 3, padding);
            Span<byte> rowSpan = row.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                this.output.Write(rowSpan);
            }

            return image.Width * image.Height * 3;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="padding">The padding bytes for each row.</param>
        /// <returns>The number of bytes written</returns>
        public int WriteGrayImageData<TPixel>(Image<TPixel> image, int padding)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer row = this.AllocateRow(image.Width, 1, padding);
            Span<byte> rowSpan = row.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                this.output.Write(rowSpan);
            }

            return image.Width * image.Height;
        }

        private IManagedByteBuffer AllocateRow(int width, int bytesPerPixel, int padding) => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, padding);

        /// <summary>
        /// Disposes <see cref="TiffWriter"/> instance, ensuring any unwritten data is flushed.
        /// </summary>
        public void Dispose()
        {
            this.output.Flush();
        }
    }
}
