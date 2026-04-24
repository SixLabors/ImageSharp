// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

/// <summary>
/// Compressor for EXR image data which does not use any compression method.
/// </summary>
internal class NoneExrCompressor : ExrBaseCompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoneExrCompressor"/> class.
    /// </summary>
    /// <param name="output">The output stream to write the compressed image data to.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">Bytes per row block.</param>
    /// <param name="bytesPerRow">Bytes per pixel row.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    public NoneExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, int width)
        : base(output, allocator, bytesPerBlock, bytesPerRow, width)
    {
    }

    /// <inheritdoc/>
    public override uint CompressRowBlock(Span<byte> rows, int rowCount)
    {
        this.Output.Write(rows);
        return (uint)rows.Length;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
