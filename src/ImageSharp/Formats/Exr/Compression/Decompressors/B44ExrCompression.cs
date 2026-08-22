// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Implementation of B44 decompressor for EXR image data.
/// </summary>
internal class B44ExrCompression : ExrBaseDecompressor
{
    private readonly int channelCount;

    // B44 encodes each 4x4 block in either 3 or 14 bytes, so both representations share this inline storage.
    private InlineArray14<byte> scratch;

    private InlineArray16<ushort> s;

    private readonly IMemoryOwner<ushort> tmpBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="B44ExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The width of a pixel row in pixels.</param>
    /// <param name="channelCount">The number of channels of the image.</param>
    public B44ExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width, int channelCount)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
        this.channelCount = channelCount;
        this.tmpBuffer = allocator.Allocate<ushort>((int)(width * rowsPerBlock * channelCount));
    }

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<ushort> outputBuffer = MemoryMarshal.Cast<byte, ushort>(buffer);
        Span<ushort> decompressed = this.tmpBuffer.GetSpan();
        Span<byte> scratch = this.scratch;
        Span<ushort> samples = this.s;
        int outputOffset = 0;
        int bytesLeft = (int)compressedBytes;

        for (int i = 0; i < this.channelCount && bytesLeft > 0; i++)
        {
            for (int y = 0; y < this.RowsPerBlock; y += 4)
            {
                Span<ushort> row0 = decompressed.Slice(outputOffset, this.Width);
                outputOffset += this.Width;
                Span<ushort> row1 = decompressed.Slice(outputOffset, this.Width);
                outputOffset += this.Width;
                Span<ushort> row2 = decompressed.Slice(outputOffset, this.Width);
                outputOffset += this.Width;
                Span<ushort> row3 = decompressed.Slice(outputOffset, this.Width);
                outputOffset += this.Width;

                int rowOffset = 0;
                for (int x = 0; x < this.Width && bytesLeft > 0; x += 4)
                {
                    int bytesRead = stream.Read(scratch[..3]);
                    if (bytesRead == 0)
                    {
                        ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream!");
                    }

                    // Check if 3-byte encoded flat field.
                    if (scratch[2] >= 13 << 2)
                    {
                        Unpack3(scratch, samples);
                        bytesLeft -= 3;
                    }
                    else
                    {
                        bytesRead = stream.Read(scratch.Slice(3, 11));
                        if (bytesRead == 0)
                        {
                            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream!");
                        }

                        Unpack14(scratch, samples);
                        bytesLeft -= 14;
                    }

                    int n = x + 3 < this.Width ? 4 : this.Width - x;
                    if (y + 3 < this.RowsPerBlock)
                    {
                        samples[..n].CopyTo(row0[rowOffset..]);
                        samples.Slice(4, n).CopyTo(row1[rowOffset..]);
                        samples.Slice(8, n).CopyTo(row2[rowOffset..]);
                        samples.Slice(12, n).CopyTo(row3[rowOffset..]);
                    }
                    else
                    {
                        samples[..n].CopyTo(row0[rowOffset..]);
                        if (y + 1 < this.RowsPerBlock)
                        {
                            samples.Slice(4, n).CopyTo(row1[rowOffset..]);
                        }

                        if (y + 2 < this.RowsPerBlock)
                        {
                            samples.Slice(8, n).CopyTo(row2[rowOffset..]);
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
        int blockSize = (int)(this.Width * this.RowsPerBlock);
        for (int y = 0; y < this.RowsPerBlock; y++)
        {
            for (int i = 0; i < this.channelCount; i++)
            {
                decompressed.Slice(offsetDecompressed + (i * blockSize), this.Width).CopyTo(outputBuffer[offsetOutput..]);
                offsetOutput += this.Width;
            }

            offsetDecompressed += this.Width;
        }
    }

    /// <summary>
    /// Unpack a 14-byte block into 4 by 4 16-bit pixels.
    /// </summary>
    /// <param name="b">The source byte data to unpack.</param>
    /// <param name="s">Destintation buffer.</param>
    private static void Unpack14(ReadOnlySpan<byte> b, Span<ushort> s)
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

    /// <summary>
    /// // Unpack a 3-byte block into 4 by 4 identical 16-bit pixels.
    /// </summary>
    /// <param name="b">The source byte data to unpack.</param>
    /// <param name="s">The destination buffer.</param>
    private static void Unpack3(ReadOnlySpan<byte> b, Span<ushort> s)
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
