// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression.Compressors
{
    internal class NoneExrCompression : ExrBaseDecompressor
    {
        public NoneExrCompression(MemoryAllocator allocator, uint uncompressedBytes)
            : base(allocator, uncompressedBytes)
        {
        }

        public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
            => stream.Read(buffer, 0, Math.Min(buffer.Length, (int)this.UncompressedBytes));

        protected override void Dispose(bool disposing)
        {
        }
    }
}
