// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

internal class NoneExrCompressor : ExrBaseCompressor
{
    public NoneExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
        : base(output, allocator, bytesPerBlock, bytesPerRow)
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
