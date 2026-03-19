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
        Stream output,
        MemoryAllocator allocator,
        int width,
        DeflateCompressionLevel compressionLevel)
    {
        switch (method)
        {
            default:
                throw ExrThrowHelper.NotSupportedCompressor(method.ToString());
        }
    }
}
