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
    /// Creates a decompressor for an EXR compression method. This method owns the list of supported compression methods.
    /// </summary>
    /// <param name="method">The compression method.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width in pixels of the image.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="channels">The channels of a row, in the order of the row.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    /// <returns>Decompressor for EXR image data.</returns>
    /// <exception cref="NotSupportedException">The decoder does not support the compression method.</exception>
    public static ExrBaseDecompressor Create(
        ExrCompression method,
        MemoryAllocator memoryAllocator,
        int width,
        uint bytesPerBlock,
        uint bytesPerRow,
        IList<ExrChannelInfo> channels,
        SegmentIntegrityHandling integrityHandling) => method switch
        {
            // The base decompressor reads the blocks that have no compression.
            ExrCompression.None => new ExrBaseDecompressor(memoryAllocator, bytesPerRow, width, integrityHandling),
            ExrCompression.Zips => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width, integrityHandling),
            ExrCompression.Zip => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, width, integrityHandling),
            ExrCompression.RunLengthEncoded => new RunLengthExrCompression(
                memoryAllocator,
                bytesPerBlock,
                bytesPerRow,
                width,
                channels[^1].PixelType == ExrPixelType.Half ? 2 : 4,
                integrityHandling),
            ExrCompression.B44 => new B44ExrCompression(memoryAllocator, bytesPerRow, width, channels, integrityHandling),
            ExrCompression.Pxr24 => new Pxr24Compression(memoryAllocator, bytesPerBlock, bytesPerRow, width, channels, integrityHandling),
            _ => throw ExrThrowHelper.NotSupportedDecompressor(method.ToString()),
        };
}
