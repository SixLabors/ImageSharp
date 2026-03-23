// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

internal static class ExrDecompressorFactory
{
    public static ExrBaseDecompressor Create(
        ExrCompression method,
        MemoryAllocator memoryAllocator,
        int width,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int channelCount)
    {
        switch (method)
        {
            case ExrCompression.None:
                return new NoneExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow);
            case ExrCompression.Zips:
                return new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow);
            case ExrCompression.Zip:
                return new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow);
            case ExrCompression.RunLengthEncoded:
                return new RunLengthExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow);
            case ExrCompression.B44:
                return new B44ExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, channelCount);
            default:
                throw ExrThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }
}
