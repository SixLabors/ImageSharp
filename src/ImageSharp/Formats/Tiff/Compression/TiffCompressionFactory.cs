// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffCompressionFactory
    {
        public static TiffBaseCompression Create(TiffCompressionType compressionType, MemoryAllocator allocator, TiffPhotometricInterpretation photometricInterpretation, int width)
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
                case TiffCompressionType.T4:
                    return new T4TiffCompression(allocator, photometricInterpretation, width);
                case TiffCompressionType.HuffmanRle:
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);
                default:
                    throw TiffThrowHelper.NotSupportedCompression(nameof(compressionType));
            }
        }
    }
}
