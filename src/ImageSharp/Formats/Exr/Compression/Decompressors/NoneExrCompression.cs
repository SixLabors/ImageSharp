// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

internal class NoneExrCompression : ExrBaseDecompressor
{
    public NoneExrCompression(MemoryAllocator allocator, uint bytesPerBlock)
        : base(allocator, bytesPerBlock)
    {
    }

    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
        => stream.Read(buffer, 0, Math.Min(buffer.Length, (int)this.BytesPerBlock));

    protected override void Dispose(bool disposing)
    {
    }
}
