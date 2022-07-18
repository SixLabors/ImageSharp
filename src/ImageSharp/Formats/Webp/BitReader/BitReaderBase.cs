// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.BitReader
{
    /// <summary>
    /// Base class for VP8 and VP8L bitreader.
    /// </summary>
    internal abstract class BitReaderBase : IDisposable
    {
        private bool isDisposed;

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
            input.Read(dataSpan.Slice(0, bytesToRead), 0, bytesToRead);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                this.Data?.Dispose();
            }

            this.isDisposed = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
