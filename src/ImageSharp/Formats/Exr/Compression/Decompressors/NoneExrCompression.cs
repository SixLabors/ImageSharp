// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

internal class NoneExrCompression : ExrBaseDecompressor
{
    public NoneExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
        : base(allocator, bytesPerBlock, bytesPerRow)
    {
    }

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        int bytesRead = stream.Read(buffer, 0, Math.Min(buffer.Length, (int)this.BytesPerBlock));
        if (bytesRead != (int)this.BytesPerBlock)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough pixel data!");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
