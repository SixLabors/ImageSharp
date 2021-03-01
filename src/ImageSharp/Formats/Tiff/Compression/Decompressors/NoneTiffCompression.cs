// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is not compressed.
    /// </summary>
    internal class NoneTiffCompression : TiffBaseDecompressor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoneTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="bitsPerPixel">The bits per pixel.</param>
        public NoneTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel)
            : base(memoryAllocator, width, bitsPerPixel)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer) => _ = stream.Read(buffer, 0, Math.Min(buffer.Length, byteCount));

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
