// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.OpenExr.Compression.Decompressors;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression;

internal static class ExrDecompressorFactory
{
    public static ExrBaseDecompressor Create(ExrCompressionType method, MemoryAllocator memoryAllocator, uint uncompressedBytes, int width, int height, uint rowsPerBlock, int channelCount)
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
            case ExrCompressionType.B44:
                return new B44Compression(memoryAllocator, uncompressedBytes, width, height, rowsPerBlock, channelCount);
            default:
                throw ExrThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }
}
