// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Decompressor for EXR image data which do not use any compression.
/// </summary>
internal class NoneExrCompression : ExrBaseDecompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoneExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per pixel row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The number of pixels per row.</param>
    public NoneExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
    }

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        int bytesRead = stream.Read(buffer[..(int)uncompressedBytes]);
        if (bytesRead != uncompressedBytes)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough pixel data from the stream!");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
