// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffWriter : IDisposable
    {
        private static readonly byte[] PaddingBytes = new byte[4];

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffWriter"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        public TiffWriter(Stream output, MemoryAllocator memoryAllocator, Configuration configuration)
        {
            this.Output = output;
            this.MemoryAllocator = memoryAllocator;
            this.Configuration = configuration;
        }

        /// <summary>
        /// Gets a value indicating whether the architecture is little-endian.
        /// </summary>
        public bool IsLittleEndian => BitConverter.IsLittleEndian;

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        public long Position => this.Output.Position;

        protected Stream Output { get; }

        protected MemoryAllocator MemoryAllocator { get; }

        protected Configuration Configuration { get; }

        /// <summary>
        /// Writes an empty four bytes to the stream, returning the offset to be written later.
        /// </summary>
        /// <returns>The offset to be written later</returns>
        public long PlaceMarker()
        {
            long offset = this.Output.Position;
            this.Write(0u);
            return offset;
        }

        /// <summary>
        /// Writes an array of bytes to the current stream.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void Write(byte[] value) => this.Output.Write(value, 0, value.Length);

        /// <summary>
        /// Writes a byte to the current stream.
        /// </summary>
        /// <param name="value">The byte to write.</param>
        public void Write(byte value) => this.Output.Write(new byte[] { value }, 0, 1);

        /// <summary>
        /// Writes a two-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The two-byte unsigned integer to write.</param>
        public void Write(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.Output.Write(bytes, 0, 2);
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void Write(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.Output.Write(bytes, 0, 4);
        }

        /// <summary>
        /// Writes an array of bytes to the current stream, padded to four-bytes.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void WritePadded(byte[] value)
        {
            this.Output.Write(value, 0, value.Length);

            if (value.Length < 4)
            {
                this.Output.Write(PaddingBytes, 0, 4 - value.Length);
            }
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the specified marker in the stream.
        /// </summary>
        /// <param name="offset">The offset returned when placing the marker</param>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void WriteMarker(long offset, uint value)
        {
            long currentOffset = this.Output.Position;
            this.Output.Seek(offset, SeekOrigin.Begin);
            this.Write(value);
            this.Output.Seek(currentOffset, SeekOrigin.Begin);
        }

        /// <summary>
        /// Disposes <see cref="TiffWriter"/> instance, ensuring any unwritten data is flushed.
        /// </summary>
        public void Dispose() => this.Output.Flush();
    }
}
