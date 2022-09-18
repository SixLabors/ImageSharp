// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression.Compressors;

internal class RunLengthCompression : ExrBaseDecompressor
{
    private readonly IMemoryOwner<byte> tmpBuffer;

    public RunLengthCompression(MemoryAllocator allocator, uint uncompressedBytes)
        : base(allocator, uncompressedBytes) => this.tmpBuffer = allocator.Allocate<byte>((int)uncompressedBytes);

    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();
        int maxLength = (int)this.UncompressedBytes;
        int offset = 0;
        while (compressedBytes > 0)
        {
            byte nextByte = ReadNextByte(stream);

            sbyte input = (sbyte)nextByte;
            if (input < 0)
            {
                int count = -input;
                compressedBytes -= (uint)(count + 1);

                if ((maxLength -= count) < 0)
                {
                    return;
                }

                // Check the input buffer is big enough to contain 'count' bytes of remaining data.
                if (compressedBytes < 0)
                {
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    uncompressed[offset + i] = ReadNextByte(stream);
                }

                offset += count;
            }
            else
            {
                int count = input;
                byte value = ReadNextByte(stream);
                compressedBytes -= 2;

                if ((maxLength -= count + 1) < 0)
                {
                    return;
                }

                // Check the input buffer is big enough to contain byte to be duplicated.
                if (compressedBytes < 0)
                {
                    return;
                }

                for (int i = 0; i < count + 1; i++)
                {
                    uncompressed[offset + i] = value;
                }

                offset += count + 1;
            }
        }

        Reconstruct(uncompressed, this.UncompressedBytes);
        Interleave(uncompressed, this.UncompressedBytes, buffer);
    }

    private static byte ReadNextByte(BufferedReadStream stream)
    {
        int nextByte = stream.ReadByte();
        if (nextByte == -1)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to decompress RLE image!");
        }

        return (byte)nextByte;
    }

    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
