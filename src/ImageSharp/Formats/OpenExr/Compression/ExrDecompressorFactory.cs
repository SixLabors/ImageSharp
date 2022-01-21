// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.OpenExr.Compression.Compressors;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression
{
    internal static class ExrDecompressorFactory
    {
        public static ExrBaseDecompressor Create(ExrCompressionType method, MemoryAllocator memoryAllocator, uint bytesPerRow)
        {
            switch (method)
            {
                case ExrCompressionType.None:
                    return new NoneExrCompression(memoryAllocator, bytesPerRow);
                case ExrCompressionType.Zips:
                    return new ZipsExrCompression(memoryAllocator, bytesPerRow);
                case ExrCompressionType.RunLengthEncoded:
                    return new RunLengthCompression(memoryAllocator, bytesPerRow);
                default:
                    throw ExrThrowHelper.NotSupportedDecompressor(nameof(method));
            }
        }
    }
}
