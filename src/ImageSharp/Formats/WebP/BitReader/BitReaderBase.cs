// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Buffers;
using System.IO;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP.BitReader
{
    /// <summary>
    /// Base class for VP8 and VP8L bitreader.
    /// </summary>
    internal abstract class BitReaderBase : IDisposable
    {
        /// <summary>
        /// Gets or sets the raw encoded image data.
        /// </summary>
        public IMemoryOwner<byte> Data { get; set; }

        /// <summary>
        /// Copies the raw encoded image data from the stream into a byte array.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="bytesToRead">Number of bytes to read as indicated from the chunk size.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        protected void ReadImageDataFromStream(Stream input, int bytesToRead, MemoryAllocator memoryAllocator)
        {
            this.Data = memoryAllocator.Allocate<byte>(bytesToRead);
            Span<byte> dataSpan = this.Data.Memory.Span;

            using (IManagedByteBuffer buffer = memoryAllocator.AllocateManagedByteBuffer(4096))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                int read;
                while (bytesToRead > 0 && (read = input.Read(buffer.Array, 0, Math.Min(bufferSpan.Length, bytesToRead))) > 0)
                {
                    buffer.Array.AsSpan(0, read).CopyTo(dataSpan);
                    bytesToRead -= read;
                    dataSpan = dataSpan.Slice(read);
                }

                if (bytesToRead > 0)
                {
                    WebPThrowHelper.ThrowImageFormatException("image file has insufficient data");
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose() => this.Data?.Dispose();
    }
}
