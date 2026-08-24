// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// Factory class for creating a compressor for EXR image data.
/// </summary>
internal static class ExrCompressorFactory
{
    /// <summary>
    /// Creates the specified exr data compressor.
    /// </summary>
    /// <param name="method">The compression method.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="output">The output stream.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="compressionLevel">The deflate compression level.</param>
    /// <returns>A compressor for EXR image data.</returns>
    public static ExrBaseCompressor Create(
        ExrCompression method,
        MemoryAllocator allocator,
        Stream output,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int width,
        DeflateCompressionLevel compressionLevel = DeflateCompressionLevel.DefaultCompression) => method switch
        {
            ExrCompression.None => new NoneExrCompressor(output, allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width),
            ExrCompression.Zips => new ZipExrCompressor(output, allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, compressionLevel),
            ExrCompression.Zip => new ZipExrCompressor(output, allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, compressionLevel),
            _ => throw ExrThrowHelper.NotSupportedCompressor(method.ToString()),
        };
}
