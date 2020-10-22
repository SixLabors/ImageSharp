// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffCompressionFactory
    {
        public static TiffBaseCompression Create(TiffCompressionType compressionType, MemoryAllocator allocator)
        {
            switch (compressionType)
            {
                case TiffCompressionType.None:
                    return new NoneTiffCompression(allocator);
                case TiffCompressionType.PackBits:
                    return new PackBitsTiffCompression(allocator);
                case TiffCompressionType.Deflate:
                    return new DeflateTiffCompression(allocator);
                case TiffCompressionType.Lzw:
                    return new LzwTiffCompression(allocator);
                default:
                    throw TiffThrowHelper.NotSupportedCompression(nameof(compressionType));
            }
        }
    }
}
