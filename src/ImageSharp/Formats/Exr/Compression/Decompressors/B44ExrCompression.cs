// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Implementation of B44 decompressor for EXR image data.
/// </summary>
internal class B44ExrCompression : ExrBaseDecompressor
{
    /// <summary>
    /// The table that restores the samples of a pLinear channel. The decoder builds it at the first use.
    /// </summary>
    private static readonly Lazy<ushort[]> LinearTable = new(CreateLinearTable);

    /// <summary>
    /// The channels of a row, in the order of the row.
    /// </summary>
    private readonly IList<ExrChannelInfo> channels;

    // B44 encodes each 4x4 block in either 3 or 14 bytes, so both representations share this inline storage.
    private InlineArray14<byte> scratch;

    private InlineArray16<ushort> tileSamples;

    /// <summary>
    /// Initializes a new instance of the <see cref="B44ExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The width of a pixel row in pixels.</param>
    /// <param name="channels">The channels of a row, in the order of the row.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public B44ExrCompression(MemoryAllocator allocator, uint bytesPerRow, int width, IList<ExrChannelInfo> channels, SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerRow, width, integrityHandling)
        => this.channels = channels;

    /// <inheritdoc/>
    /// <remarks>
    /// The chunk header check proves that all <paramref name="compressedBytes"/> bytes are in the stream. Thus each read in this method gets all its bytes.
    /// </remarks>
    protected override bool DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        // The output holds 2-byte units. A HALF sample uses one unit. A UINT or FLOAT sample uses two units. Each channel offset and each channel width in this
        // method counts these units.
        int samplesPerRow = (int)(this.BytesPerRow / sizeof(ushort));
        int rows = (int)(uncompressedBytes / this.BytesPerRow);
        Span<ushort> output = MemoryMarshal.Cast<byte, ushort>(buffer[..(int)uncompressedBytes]);
        Span<ushort> samples = this.tileSamples;
        int lastChannel = this.channels.Count - 1;
        int channelOffset = 0;
        for (int channel = 0; channel <= lastChannel; channel++)
        {
            ExrChannelInfo channelInfo = this.channels[channel];
            if (channelInfo.PixelType != ExrPixelType.Half)
            {
                // B44 compresses only HALF channels. The block stores the samples of other channels without change, one row after the other.
                int channelWidth = this.Width * 2;
                uint rowBytes = (uint)channelWidth * sizeof(ushort);
                int completeRows = (int)Math.Min((uint)rows, compressedBytes / rowBytes);
                for (int y = 0; y < completeRows; y++)
                {
                    stream.ReadExactly(MemoryMarshal.AsBytes(output.Slice((y * samplesPerRow) + channelOffset, channelWidth)));
                }

                if (completeRows < rows)
                {
                    // In the IgnoreImageData mode, the block keeps the complete rows. The method clears the other samples.
                    this.IntegrityHandling.ThrowOrIgnoreImageDataError("The B44 EXR block ends before all pixel data is available.");
                    ClearRemainingSamples(output, samplesPerRow, rows, channelOffset, channelWidth, completeRows, 0);

                    // A pixel is complete only when its sample of the last channel is complete.
                    this.SetIncompleteBlock(channel == lastChannel ? completeRows : 0, 0, 0);
                    return true;
                }

                compressedBytes -= (uint)rows * rowBytes;
                channelOffset += channelWidth;
                continue;
            }

            // The encoder stores exp(x / 8) for each sample x of a pLinear channel.
            ushort[]? linearTable = channelInfo.Linear != 0 ? LinearTable.Value : null;
            for (int y = 0; y < rows; y += 4)
            {
                int tileHeight = Math.Min(4, rows - y);
                for (int x = 0; x < this.Width; x += 4)
                {
                    if (!this.TryReadTile(stream, ref compressedBytes, samples))
                    {
                        // In the IgnoreImageData mode, the block keeps the complete tiles. The method clears the other samples.
                        this.IntegrityHandling.ThrowOrIgnoreImageDataError("The B44 EXR block ends before all pixel data is available.");
                        ClearRemainingSamples(output, samplesPerRow, rows, channelOffset, this.Width, y, x);

                        // The tiles of each channel come after the tiles of the channel before it. A pixel is complete
                        // only when its tile of the last channel is complete. Thus only a failure in the last channel
                        // leaves complete pixels: the rows above this tile row, and the pixels left of this tile.
                        if (channel == lastChannel)
                        {
                            this.SetIncompleteBlock(y, tileHeight, x);
                        }
                        else
                        {
                            this.SetIncompleteBlock(0, 0, 0);
                        }

                        return true;
                    }

                    if (linearTable is not null)
                    {
                        for (int i = 0; i < samples.Length; i++)
                        {
                            samples[i] = linearTable[samples[i]];
                        }
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

            channelOffset += this.Width;
        }

        return true;
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
        if (remaining < 3)
        {
            return false;
        }

        stream.ReadExactly(bytes[..3]);
        remaining -= 3;
        if (bytes[2] >= 13 << 2)
        {
            Unpack3(bytes, samples);
            return true;
        }

        if (remaining < 11)
        {
            return false;
        }

        stream.ReadExactly(bytes.Slice(3, 11));
        remaining -= 11;
        Unpack14(bytes, samples);
        return true;
    }

    /// <summary>
    /// Clears the samples from the first incomplete tile or row to the end of the block.
    /// </summary>
    /// <param name="output">The row-major buffer of 2-byte units.</param>
    /// <param name="samplesPerRow">The number of 2-byte units across all channels in a row.</param>
    /// <param name="rows">The number of visible rows in the block.</param>
    /// <param name="channelOffset">The first 2-byte unit of the incomplete channel in a row.</param>
    /// <param name="channelWidth">The number of 2-byte units of the incomplete channel in a row.</param>
    /// <param name="y">The first row of the incomplete tile or row.</param>
    /// <param name="x">The first column of the incomplete tile.</param>
    private static void ClearRemainingSamples(Span<ushort> output, int samplesPerRow, int rows, int channelOffset, int channelWidth, int y, int x)
    {
        // Earlier tiles in this four-row band filled the samples to the left of x. Later bands in this channel contain old buffer data.
        for (int row = y; row < rows; row++)
        {
            int start = row - y < 4 ? x : 0;
            output.Slice((row * samplesPerRow) + channelOffset + start, channelWidth - start).Clear();
        }

        // The channels after the incomplete channel fill the rest of each row. They also contain old buffer data.
        int channelEnd = channelOffset + channelWidth;
        for (int row = 0; row < rows; row++)
        {
            output.Slice((row * samplesPerRow) + channelEnd, samplesPerRow - channelEnd).Clear();
        }
    }

    /// <summary>
    /// Creates the table that restores the samples of a pLinear channel. The encoder stores exp(x / 8) for each sample x. Thus the entry of a stored sample s
    /// is 8 * ln(s). Infinity, NaN and negative values other than -0 have no logarithm. Their entries are 0.
    /// </summary>
    /// <returns>The table, with one entry for each HALF bit pattern.</returns>
    private static ushort[] CreateLinearTable()
    {
        ushort[] table = new ushort[ushort.MaxValue + 1];
        for (int i = 0; i < table.Length; i++)
        {
            table[i] = (i & 0x7C00) == 0x7C00 || i > 0x8000 ? (ushort)0 : HalfTypeHelper.Pack(8 * MathF.Log(HalfTypeHelper.Unpack((ushort)i)));
        }

        return table;
    }

    /// <summary>
    /// Unpack a 14-byte block into 4 by 4 16-bit pixels.
    /// </summary>
    /// <param name="b">The source byte data to unpack.</param>
    /// <param name="samples">The destination for the tile samples.</param>
    private static void Unpack14(ReadOnlySpan<byte> b, Span<ushort> samples)
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
    private static void Unpack3(ReadOnlySpan<byte> b, Span<ushort> samples)
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
}
