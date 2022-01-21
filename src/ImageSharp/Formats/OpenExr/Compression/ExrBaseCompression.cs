// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression
{
    internal abstract class ExrBaseCompression : IDisposable
    {
        private bool isDisposed;

        protected ExrBaseCompression(MemoryAllocator allocator, uint bytePerRow)
        {
            this.Allocator = allocator;
            this.UncompressedBytes = bytePerRow;
        }

        /// <summary>
        /// Gets the memory allocator.
        /// </summary>
        protected MemoryAllocator Allocator { get; }

        /// <summary>
        /// Gets the uncompressed bytes.
        /// </summary>
        public uint UncompressedBytes { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.Dispose(true);
        }

        protected abstract void Dispose(bool disposing);
    }
}
