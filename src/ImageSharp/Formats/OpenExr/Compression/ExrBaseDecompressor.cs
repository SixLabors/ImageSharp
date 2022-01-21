// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression
{
    internal abstract class ExrBaseDecompressor : ExrBaseCompression
    {
        protected ExrBaseDecompressor(MemoryAllocator allocator, uint bytePerRow)
            : base(allocator, bytePerRow)
        {
        }

        public abstract void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer);
    }
}
