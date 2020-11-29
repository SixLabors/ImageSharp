// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Class to handle cases where TIFF image data is not compressed.
    /// </summary>
    internal class NoneTiffCompression : TiffBaseCompression
    {
        public NoneTiffCompression(MemoryAllocator allocator)
            : base(allocator)
        {
        }

        /// <inheritdoc/>
        public override void Decompress(Stream stream, int byteCount, Span<byte> buffer)
        {
            stream.ReadFull(buffer, byteCount);
        }
    }
}
