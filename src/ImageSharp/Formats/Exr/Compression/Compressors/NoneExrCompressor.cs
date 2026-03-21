// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

internal class NoneExrCompressor : ExrBaseCompressor
{
    public NoneExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock)
        : base(output, allocator, bytesPerBlock)
    {
    }

    /// <inheritdoc/>
    public override ExrCompression Method => ExrCompression.Zip;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerBlock)
    {
    }

    /// <inheritdoc/>
    public override uint CompressRowBlock(Span<byte> rows, int height)
    {
        this.Output.Write(rows);
        return (uint)rows.Length;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
