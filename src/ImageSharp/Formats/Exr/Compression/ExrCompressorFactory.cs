// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

internal static class ExrCompressorFactory
{
    public static ExrBaseCompressor Create(
        ExrCompression method,
        MemoryAllocator allocator,
        Stream output,
        uint bytesPerBlock,
        uint bytesPerRow,
        DeflateCompressionLevel compressionLevel = DeflateCompressionLevel.DefaultCompression)
    {
        switch (method)
        {
            case ExrCompression.None:
                return new NoneExrCompressor(output, allocator, bytesPerBlock, bytesPerRow);
            case ExrCompression.Zips:
                return new ZipExrCompressor(output, allocator, bytesPerBlock, bytesPerRow, compressionLevel);
            case ExrCompression.Zip:
                return new ZipExrCompressor(output, allocator, bytesPerBlock, bytesPerRow, compressionLevel);

            default:
                throw ExrThrowHelper.NotSupportedCompressor(method.ToString());
        }
    }
}
