// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

    private InlineArray16<ushort> tileSamples;

    private readonly bool preservePartialData;

    /// <summary>
    /// Initializes a new instance of the <see cref="B44ExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The width of a pixel row in pixels.</param>
    /// <param name="channelCount">The number of channels of the image.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public B44ExrCompression(
        MemoryAllocator allocator,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int width,
        int channelCount,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
        this.channelCount = channelCount;
        this.preservePartialData = integrityHandling == SegmentIntegrityHandling.IgnoreImageData;
    }

    /// <inheritdoc/>
    protected override void DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        int samplesPerRow = (int)(this.BytesPerRow / sizeof(ushort));
        int rows = (int)(uncompressedBytes / this.BytesPerRow);
        Span<ushort> output = MemoryMarshal.Cast<byte, ushort>(buffer[..(int)uncompressedBytes]);
        Span<ushort> samples = this.tileSamples;
        for (int channel = 0; channel < this.channelCount; channel++)
        {
            int channelOffset = channel * this.Width;
            for (int y = 0; y < rows; y += 4)
            {
                int tileHeight = Math.Min(4, rows - y);
                for (int x = 0; x < this.Width; x += 4)
                {
                    if (!this.TryReadTile(stream, ref compressedBytes, samples))
                    {
                        if (!this.preservePartialData)
                        {
                            ExrThrowHelper.ThrowInvalidImageContentException("The B44 EXR block ends before all pixel data is available.");
                        }

                        this.ClearRemainingSamples(output, samplesPerRow, rows, channel, y, x);
                        return;
                    }

                    // Input is grouped by channel. Output is grouped by row, with each channel in a separate span.
                    // Copy only visible samples from complete tiles, including at the right and bottom edges.
                    int tileWidth = Math.Min(4, this.Width - x);
                    int destination = (y * samplesPerRow) + channelOffset + x;
                    for (int row = 0; row < tileHeight; row++)
                    {
                        samples.Slice(row * 4, tileWidth).CopyTo(output.Slice(destination, tileWidth));
                        destination += samplesPerRow;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads and unpacks one complete B44 tile within the declared block.
    /// </summary>
    /// <param name="stream">The stream positioned at the tile.</param>
    /// <param name="remaining">The remaining encoded bytes in the block.</param>
    /// <param name="samples">The destination for the sixteen tile samples.</param>
    /// <returns>Whether the complete tile was read.</returns>
    private bool TryReadTile(BufferedReadStream stream, ref uint remaining, Span<ushort> samples)
    {
        Span<byte> bytes = this.scratch;
        if (remaining < 3 || stream.ReadAtLeast(bytes[..3], 3, throwOnEndOfStream: false) != 3)
        {
            return false;
        }

        remaining -= 3;
        if (bytes[2] >= 13 << 2)
        {
            Unpack3(bytes, samples);
            return true;
        }

        if (remaining < 11 || stream.ReadAtLeast(bytes.Slice(3, 11), 11, throwOnEndOfStream: false) != 11)
        {
            return false;
        }

        remaining -= 11;
        Unpack14(bytes, samples);
        return true;
    }

    /// <summary>
    /// Clears the samples from the first incomplete tile through the end of the block.
    /// </summary>
    /// <param name="output">The row-major sample buffer.</param>
    /// <param name="samplesPerRow">The sample count across all channels in a row.</param>
    /// <param name="rows">The number of visible rows in the block.</param>
    /// <param name="channel">The channel of the incomplete tile.</param>
    /// <param name="y">The first row of the incomplete tile.</param>
    /// <param name="x">The first column of the incomplete tile.</param>
    private void ClearRemainingSamples(Span<ushort> output, int samplesPerRow, int rows, int channel, int y, int x)
    {
        // Earlier tiles in this four-row band already filled the samples to the left of x.
        // Later bands in this channel and all later channels still contain old buffer data.
        int channelOffset = channel * this.Width;
        for (int row = y; row < rows; row++)
        {
            int start = row - y < 4 ? x : 0;
            output.Slice((row * samplesPerRow) + channelOffset + start, this.Width - start).Clear();
        }

        int remainingChannels = this.channelCount - channel - 1;
        for (int row = 0; row < rows; row++)
        {
            output.Slice((row * samplesPerRow) + channelOffset + this.Width, remainingChannels * this.Width).Clear();
        }
    }

    /// <summary>
    /// Unpack a 14-byte block into 4 by 4 16-bit pixels.
    /// </summary>
    /// <param name="b">The source byte data to unpack.</param>
    /// <param name="samples">The destination for the tile samples.</param>
    private static void Unpack14(Span<byte> b, Span<ushort> samples)
    {
        samples[0] = (ushort)((b[0] << 8) | b[1]);

        ushort shift = (ushort)(b[2] >> 2);
        ushort bias = (ushort)(0x20u << shift);

        samples[4] = (ushort)(samples[0] + ((((b[2] << 4) | (b[3] >> 4)) & 0x3fu) << shift) - bias);
        samples[8] = (ushort)(samples[4] + ((((b[3] << 2) | (b[4] >> 6)) & 0x3fu) << shift) - bias);
        samples[12] = (ushort)(samples[8] + ((b[4] & 0x3fu) << shift) - bias);

        samples[1] = (ushort)(samples[0] + ((uint)(b[5] >> 2) << shift) - bias);
        samples[5] = (ushort)(samples[4] + ((((b[5] << 4) | (b[6] >> 4)) & 0x3fu) << shift) - bias);
        samples[9] = (ushort)(samples[8] + ((((b[6] << 2) | (b[7] >> 6)) & 0x3fu) << shift) - bias);
        samples[13] = (ushort)(samples[12] + ((b[7] & 0x3fu) << shift) - bias);

        samples[2] = (ushort)(samples[1] + ((uint)(b[8] >> 2) << shift) - bias);
        samples[6] = (ushort)(samples[5] + ((((b[8] << 4) | (b[9] >> 4)) & 0x3fu) << shift) - bias);
        samples[10] = (ushort)(samples[9] + ((((b[9] << 2) | (b[10] >> 6)) & 0x3fu) << shift) - bias);
        samples[14] = (ushort)(samples[13] + ((b[10] & 0x3fu) << shift) - bias);

        samples[3] = (ushort)(samples[2] + ((uint)(b[11] >> 2) << shift) - bias);
        samples[7] = (ushort)(samples[6] + ((((b[11] << 4) | (b[12] >> 4)) & 0x3fu) << shift) - bias);
        samples[11] = (ushort)(samples[10] + ((((b[12] << 2) | (b[13] >> 6)) & 0x3fu) << shift) - bias);
        samples[15] = (ushort)(samples[14] + ((b[13] & 0x3fu) << shift) - bias);

        for (int i = 0; i < 16; ++i)
        {
            if ((samples[i] & 0x8000) != 0)
            {
                samples[i] &= 0x7fff;
            }
            else
            {
                samples[i] = (ushort)~samples[i];
            }
        }
    }

    /// <summary>
    /// // Unpack a 3-byte block into 4 by 4 identical 16-bit pixels.
    /// </summary>
    /// <param name="b">The source byte data to unpack.</param>
    /// <param name="samples">The destination buffer.</param>
    private static void Unpack3(Span<byte> b, Span<ushort> samples)
    {
        samples[0] = (ushort)((b[0] << 8) | b[1]);

        if ((samples[0] & 0x8000) != 0)
        {
            samples[0] &= 0x7fff;
        }
        else
        {
            samples[0] = (ushort)~samples[0];
        }

        for (int i = 1; i < 16; ++i)
        {
            samples[i] = samples[0];
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
