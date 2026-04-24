// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// The Factory class for creating a EXR data decompressor.
/// </summary>
internal static class ExrDecompressorFactory
{
    /// <summary>
    /// Creates a decomprssor for a specific EXR compression type.
    /// </summary>
    /// <param name="method">The compression method.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width in pixels of the image.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The rows per block.</param>
    /// <param name="channelCount">The number of image channels.</param>
    /// <returns>Decompressor for EXR image data.</returns>
    public static ExrBaseDecompressor Create(
        ExrCompression method,
        MemoryAllocator memoryAllocator,
        int width,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int channelCount) => method switch
        {
            ExrCompression.None => new NoneExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width),
            ExrCompression.Zips => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width),
            ExrCompression.Zip => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width),
            ExrCompression.RunLengthEncoded => new RunLengthExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width),
            ExrCompression.B44 => new B44ExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, channelCount),
            ExrCompression.Pxr24 => new Pxr24Compression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, channelCount),
            _ => throw ExrThrowHelper.NotSupportedDecompressor(nameof(method)),
        };
}
