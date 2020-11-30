// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal static class TiffCompressionFactory
    {
        public static TiffBaseCompression Create(TiffDecoderCompressionType compressionType, MemoryAllocator allocator, TiffPhotometricInterpretation photometricInterpretation, int width)
        {
            switch (compressionType)
            {
                case TiffDecoderCompressionType.None:
                    return new NoneTiffCompression(allocator);
                case TiffDecoderCompressionType.PackBits:
                    return new PackBitsTiffCompression(allocator);
                case TiffDecoderCompressionType.Deflate:
                    return new DeflateTiffCompression(allocator);
                case TiffDecoderCompressionType.Lzw:
                    return new LzwTiffCompression(allocator);
                case TiffDecoderCompressionType.T4:
                    return new T4TiffCompression(allocator, photometricInterpretation, width);
                case TiffDecoderCompressionType.HuffmanRle:
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);
                default:
                    throw TiffThrowHelper.NotSupportedCompression(nameof(compressionType));
            }
        }
    }
}
