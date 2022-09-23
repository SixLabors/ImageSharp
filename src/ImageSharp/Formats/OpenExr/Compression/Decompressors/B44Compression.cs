// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.OpenExr.Compression.Decompressors;

internal class B44Compression : ExrBaseDecompressor
{
    private readonly int width;

    private readonly int height;

    private readonly uint rowsPerBlock;

    private readonly int channelCount;

    private byte[] scratch = new byte[14];

    private ushort[] s = new ushort[16];

    private IMemoryOwner<ushort> tmpBuffer;

    public B44Compression(MemoryAllocator allocator, uint uncompressedBytes, int width, int height, uint rowsPerBlock, int channelCount)
        : base(allocator, uncompressedBytes)
    {
        this.width = width;
        this.height = height;
        this.rowsPerBlock = rowsPerBlock;
        this.channelCount = channelCount;
        this.tmpBuffer = allocator.Allocate<ushort>((int)(width * rowsPerBlock * channelCount));
    }

    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<ushort> outputBuffer = MemoryMarshal.Cast<byte, ushort>(buffer);
        Span<ushort> decompressed = this.tmpBuffer.GetSpan();
        int outputOffset = 0;
        int bytesLeft = (int)compressedBytes;
        for (int i = 0; i < this.channelCount && bytesLeft > 0; i++)
        {
            for (int y = 0; y < this.rowsPerBlock; y += 4)
            {
                Span<ushort> row0 = decompressed.Slice(outputOffset, this.width);
                outputOffset += this.width;
                Span<ushort> row1 = decompressed.Slice(outputOffset, this.width);
                outputOffset += this.width;
                Span<ushort> row2 = decompressed.Slice(outputOffset, this.width);
                outputOffset += this.width;
                Span<ushort> row3 = decompressed.Slice(outputOffset, this.width);
                outputOffset += this.width;

                int rowOffset = 0;
                for (int x = 0; x < this.width && bytesLeft > 0; x += 4)
                {
                    int bytesRead = stream.Read(this.scratch, 0, 3);
                    if (bytesRead == 0)
                    {
                        ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream");
                    }

                    if (this.scratch[2] >= 13 << 2)
                    {
                        Unpack3(this.scratch, this.s);
                        bytesLeft -= 3;
                    }
                    else
                    {
                        bytesRead = stream.Read(this.scratch, 3, 11);
                        if (bytesRead == 0)
                        {
                            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream");
                        }

                        Unpack14(this.scratch, this.s);
                        bytesLeft -= 14;
                    }

                    int n = x + 3 < this.width ? 4 : this.width - x;
                    if (y + 3 < this.rowsPerBlock)
                    {
                        this.s.AsSpan(0, n).CopyTo(row0.Slice(rowOffset));
                        this.s.AsSpan(4, n).CopyTo(row1.Slice(rowOffset));
                        this.s.AsSpan(8, n).CopyTo(row2.Slice(rowOffset));
                        this.s.AsSpan(12, n).CopyTo(row3.Slice(rowOffset));
                    }
                    else
                    {
                        this.s.AsSpan(0, n).CopyTo(row0.Slice(rowOffset));
                        if (y + 1 < this.rowsPerBlock)
                        {
                            this.s.AsSpan(4, n).CopyTo(row1.Slice(rowOffset));
                        }

                        if (y + 2 < this.rowsPerBlock)
                        {
                            this.s.AsSpan(8, n).CopyTo(row2.Slice(rowOffset));
                        }
                    }

                    rowOffset += 4;
                }

                if (bytesLeft <= 0)
                {
                    break;
                }
            }
        }

        // Rearrange the decompressed data such that the data for each scan line form a contiguous block.
        int offsetDecompressed = 0;
        int offsetOutput = 0;
        int blockSize = (int)(this.width * this.rowsPerBlock);
        for (int y = 0; y < this.rowsPerBlock; y++)
        {
            for (int i = 0; i < this.channelCount; i++)
            {
                decompressed.Slice(offsetDecompressed + (i * blockSize), this.width).CopyTo(outputBuffer.Slice(offsetOutput));
                offsetOutput += this.width;
            }

            offsetDecompressed += this.width;
        }
    }

    // Unpack a 14-byte block into 4 by 4 16-bit pixels.
    private static void Unpack14(Span<byte> b, Span<ushort> s)
    {
        s[0] = (ushort)((b[0] << 8) | b[1]);

        ushort shift = (ushort)(b[2] >> 2);
        ushort bias = (ushort)(0x20u << shift);

        s[4] = (ushort)(s[0] + ((((b[2] << 4) | (b[3] >> 4)) & 0x3fu) << shift) - bias);
        s[8] = (ushort)(s[4] + ((((b[3] << 2) | (b[4] >> 6)) & 0x3fu) << shift) - bias);
        s[12] = (ushort)(s[8] + ((b[4] & 0x3fu) << shift) - bias);

        s[1] = (ushort)(s[0] + ((uint)(b[5] >> 2) << shift) - bias);
        s[5] = (ushort)(s[4] + ((((b[5] << 4) | (b[6] >> 4)) & 0x3fu) << shift) - bias);
        s[9] = (ushort)(s[8] + ((((b[6] << 2) | (b[7] >> 6)) & 0x3fu) << shift) - bias);
        s[13] = (ushort)(s[12] + ((b[7] & 0x3fu) << shift) - bias);

        s[2] = (ushort)(s[1] + ((uint)(b[8] >> 2) << shift) - bias);
        s[6] = (ushort)(s[5] + ((((b[8] << 4) | (b[9] >> 4)) & 0x3fu) << shift) - bias);
        s[10] = (ushort)(s[9] + ((((b[9] << 2) | (b[10] >> 6)) & 0x3fu) << shift) - bias);
        s[14] = (ushort)(s[13] + ((b[10] & 0x3fu) << shift) - bias);

        s[3] = (ushort)(s[2] + ((uint)(b[11] >> 2) << shift) - bias);
        s[7] = (ushort)(s[6] + ((((b[11] << 4) | (b[12] >> 4)) & 0x3fu) << shift) - bias);
        s[11] = (ushort)(s[10] + ((((b[12] << 2) | (b[13] >> 6)) & 0x3fu) << shift) - bias);
        s[15] = (ushort)(s[14] + ((b[13] & 0x3fu) << shift) - bias);

        for (int i = 0; i < 16; ++i)
        {
            if ((s[i] & 0x8000) != 0)
            {
                s[i] &= 0x7fff;
            }
            else
            {
                s[i] = (ushort)~s[i];
            }
        }
    }

    // Unpack a 3-byte block into 4 by 4 identical 16-bit pixels.
    private static void Unpack3(Span<byte> b, Span<ushort> s)
    {
        s[0] = (ushort)((b[0] << 8) | b[1]);

        if ((s[0] & 0x8000) != 0)
        {
            s[0] &= 0x7fff;
        }
        else
        {
            s[0] = (ushort)~s[0];
        }

        for (int i = 1; i < 16; ++i)
        {
            s[i] = s[0];
        }
    }

    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
