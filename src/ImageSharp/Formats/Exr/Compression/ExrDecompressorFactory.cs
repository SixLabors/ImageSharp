// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.IO;
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
    /// <param name="pixelType">The pixel type.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    /// <returns>Decompressor for EXR image data.</returns>
    public static ExrBaseDecompressor Create(
        ExrCompression method,
        MemoryAllocator memoryAllocator,
        int width,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int channelCount,
        ExrPixelType pixelType,
        SegmentIntegrityHandling integrityHandling) => method switch
        {
            ExrCompression.None => new NoneExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width),
            ExrCompression.Zips => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width),
            ExrCompression.Zip => new ZipExrCompression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width),
            ExrCompression.RunLengthEncoded => new RunLengthExrCompression(
                memoryAllocator,
                bytesPerBlock,
                bytesPerRow,
                rowsPerBlock,
                width,
                pixelType == ExrPixelType.Half ? 2 : 4,
                integrityHandling),

            // The B44 decoder needs one HALF sample for every channel in each row.
            ExrCompression.B44 when pixelType == ExrPixelType.Half
                && (ulong)width * (uint)channelCount * sizeof(ushort) == bytesPerRow => new B44ExrCompression(
                memoryAllocator,
                bytesPerBlock,
                bytesPerRow,
                rowsPerBlock,
                width,
                channelCount,
                integrityHandling),

            // The base decompressor still reads blocks stored without compression.
            ExrCompression.B44 => new RawB44Decompressor(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width),
            ExrCompression.Pxr24 => new Pxr24Compression(memoryAllocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width, channelCount, pixelType),
            _ => throw ExrThrowHelper.NotSupportedDecompressor(nameof(method)),
        };

    /// <summary>
    /// Reads raw blocks from a B44 image whose channel layout the compressed decoder cannot handle.
    /// </summary>
    private sealed class RawB44Decompressor : ExrBaseDecompressor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawB44Decompressor"/> class.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        /// <param name="bytesPerBlock">The maximum decoded bytes per block.</param>
        /// <param name="bytesPerRow">The decoded bytes per row.</param>
        /// <param name="rowsPerBlock">The maximum rows per block.</param>
        /// <param name="width">The image width in pixels.</param>
        public RawB44Decompressor(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
            : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
        {
        }

        /// <inheritdoc/>
        protected override void DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
            => ExrThrowHelper.ThrowNotSupported("The B44 EXR block has an unsupported channel layout.");

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
