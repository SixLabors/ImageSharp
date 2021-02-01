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
    internal class NoneTiffCompression : TiffBaseCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoneTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        public NoneTiffCompression(MemoryAllocator memoryAllocator)
            : base(memoryAllocator)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer) => _ = stream.Read(buffer, 0, byteCount);
    }
}
