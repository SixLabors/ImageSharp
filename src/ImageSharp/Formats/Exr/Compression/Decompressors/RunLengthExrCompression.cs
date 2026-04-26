// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Implementation of RLE decompressor for EXR images.
/// </summary>
internal class RunLengthExrCompression : ExrBaseDecompressor
{
    private readonly IMemoryOwner<byte> tmpBuffer;

    private readonly ushort[] s = new ushort[16];

    /// <summary>
    /// Initializes a new instance of the <see cref="RunLengthExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    public RunLengthExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width) => this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();
        int maxLength = (int)this.BytesPerBlock;
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

                for (int i = 0; i < count + 1; i++)
                {
                    uncompressed[offset + i] = value;
                }

                offset += count + 1;
            }
        }

        Reconstruct(uncompressed, this.BytesPerBlock);
        Interleave(uncompressed, this.BytesPerBlock, buffer);
    }

    /// <summary>
    /// Reads the next byte from the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The next byte.</returns>
    private static byte ReadNextByte(BufferedReadStream stream)
    {
        int nextByte = stream.ReadByte();
        if (nextByte == -1)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to decompress RLE encoded EXR image!");
        }

        return (byte)nextByte;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
