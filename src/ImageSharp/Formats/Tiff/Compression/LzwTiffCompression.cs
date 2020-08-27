// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using LZW compression.
    /// </summary>
    internal class LzwTiffCompression : TiffBaseCompression
    {
        public LzwTiffCompression(MemoryAllocator allocator)
            : base(allocator)
        {
        }

        /// <inheritdoc/>
        public override void Decompress(Stream stream, int byteCount, Span<byte> buffer)
        {
            var subStream = new SubStream(stream, byteCount);
            using (var decoder = new TiffLzwDecoder(subStream))
            {
                decoder.DecodePixels(buffer.Length, 8, buffer);
            }
        }
    }
}
