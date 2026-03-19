// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

internal static class ExrDecompressorFactory
{
    public static ExrBaseDecompressor Create(ExrCompression method, MemoryAllocator memoryAllocator, uint bytesPerBlock, int width, int height, uint rowsPerBlock, int channelCount)
    {
        switch (method)
        {
            case ExrCompression.None:
                return new NoneExrCompression(memoryAllocator, bytesPerBlock);
            case ExrCompression.Zips:
                return new ZipExrCompression(memoryAllocator, bytesPerBlock);
            case ExrCompression.Zip:
                return new ZipExrCompression(memoryAllocator, bytesPerBlock);
            case ExrCompression.RunLengthEncoded:
                return new RunLengthCompression(memoryAllocator, bytesPerBlock);
            case ExrCompression.B44:
                return new B44Compression(memoryAllocator, bytesPerBlock, width, height, rowsPerBlock, channelCount);
            default:
                throw ExrThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }
}
