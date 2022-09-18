// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.OpenExr.Compression.Compressors;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression;

internal static class ExrDecompressorFactory
{
    public static ExrBaseDecompressor Create(ExrCompressionType method, MemoryAllocator memoryAllocator, uint uncompressedBytes)
    {
        switch (method)
        {
            case ExrCompressionType.None:
                return new NoneExrCompression(memoryAllocator, uncompressedBytes);
            case ExrCompressionType.Zips:
                return new ZipExrCompression(memoryAllocator, uncompressedBytes);
            case ExrCompressionType.Zip:
                return new ZipExrCompression(memoryAllocator, uncompressedBytes);
            case ExrCompressionType.RunLengthEncoded:
                return new RunLengthCompression(memoryAllocator, uncompressedBytes);
            default:
                throw ExrThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }
}
