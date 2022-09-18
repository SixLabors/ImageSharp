// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression;

internal abstract class ExrBaseDecompressor : ExrBaseCompression
{
    protected ExrBaseDecompressor(MemoryAllocator allocator, uint bytePerRow)
        : base(allocator, bytePerRow)
    {
    }

    public abstract void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer);

    protected static void Reconstruct(Span<byte> buffer, uint unCompressedBytes)
    {
        int offset = 0;
        for (int i = 0; i < unCompressedBytes - 1; i++)
        {
            byte d = (byte)(buffer[offset] + (buffer[offset + 1] - 128));
            buffer[offset + 1] = d;
            offset++;
        }
    }

    protected static void Interleave(Span<byte> source, uint unCompressedBytes, Span<byte> output)
    {
        int sourceOffset = 0;
        int offset0 = 0;
        int offset1 = (int)((unCompressedBytes + 1) / 2);
        while (sourceOffset < unCompressedBytes)
        {
            output[sourceOffset++] = source[offset0++];
            output[sourceOffset++] = source[offset1++];
        }
    }
}
