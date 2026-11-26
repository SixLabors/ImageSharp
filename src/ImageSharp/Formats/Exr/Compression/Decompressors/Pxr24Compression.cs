// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Implementation of PXR24 decompressor for EXR image data.
/// </summary>
internal class Pxr24Compression : ExrBaseDecompressor
{
    private readonly IMemoryOwner<byte> tmpBuffer;

    /// <summary>
    /// The pixel type of each channel of a row, in the order of the row.
    /// </summary>
    private readonly ExrPixelType[] channelTypes;

    /// <summary>
    /// The number of packed bytes of a row before decompression.
    /// </summary>
    private readonly uint packedBytesPerRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pxr24Compression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per pixel row.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="channels">The channels of a row, in the order of the row.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public Pxr24Compression(
        MemoryAllocator allocator,
        uint bytesPerBlock,
        uint bytesPerRow,
        int width,
        IList<ExrChannelInfo> channels,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerRow, width, integrityHandling)
    {
        this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.channelTypes = new ExrPixelType[channels.Count];

        // PXR24 keeps 4 bytes of a UINT sample and 2 bytes of a HALF sample. It keeps only the 3 high bytes of a FLOAT sample. Thus a packed row has one byte
        // less for each FLOAT sample.
        uint packedBytesPerRow = bytesPerRow;
        for (int i = 0; i < channels.Count; i++)
        {
            ExrPixelType pixelType = channels[i].PixelType;
            this.channelTypes[i] = pixelType;
            if (pixelType == ExrPixelType.Float)
            {
                packedBytesPerRow -= (uint)width;
            }
        }

        this.packedBytesPerRow = packedBytesPerRow;
    }

    /// <inheritdoc/>
    protected override bool DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        uint rowCount = uncompressedBytes / this.BytesPerRow;
        uint packedBytes = rowCount * this.packedBytesPerRow;
        Span<byte> uncompressed = this.tmpBuffer.GetSpan()[..(int)packedBytes];

        if (!this.UndoZipCompression(stream, compressedBytes, uncompressed, packedBytes))
        {
            return false;
        }

        // A row holds the samples of each channel in turn. Each channel uses its own sample type. The output of a channel starts at a byte offset. A HALF
        // channel before a 4-byte channel can put that offset 2 bytes after a 4-byte boundary. Thus each channel writes to its own byte slice.
        int lastIn = 0;
        int outputOffset = 0;
        for (uint y = 0; y < rowCount; y++)
        {
            for (int c = 0; c < this.channelTypes.Length; c++)
            {
                switch (this.channelTypes[c])
                {
                    case ExrPixelType.UnsignedInt:
                    {
                        int offsetT0 = lastIn;
                        lastIn += this.Width;
                        int offsetT1 = lastIn;
                        lastIn += this.Width;
                        int offsetT2 = lastIn;
                        lastIn += this.Width;
                        int offsetT3 = lastIn;
                        lastIn += this.Width;

                        Span<uint> output = MemoryMarshal.Cast<byte, uint>(buffer.Slice(outputOffset, this.Width * sizeof(uint)));
                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint t2 = uncompressed[offsetT2];
                            uint t3 = uncompressed[offsetT3];
                            uint diff = (t0 << 24) | (t1 << 16) | (t2 << 8) | t3;

                            pixel += diff;
                            output[x] = pixel;

                            offsetT0++;
                            offsetT1++;
                            offsetT2++;
                            offsetT3++;
                        }

                        outputOffset += output.Length * sizeof(uint);

                        break;
                    }

                    case ExrPixelType.Half:
                    {
                        int offsetT0 = lastIn;
                        lastIn += this.Width;
                        int offsetT1 = lastIn;
                        lastIn += this.Width;

                        Span<ushort> output = MemoryMarshal.Cast<byte, ushort>(buffer.Slice(outputOffset, this.Width * sizeof(ushort)));
                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint diff = (t0 << 8) | t1;

                            pixel += diff;
                            output[x] = (ushort)pixel;

                            offsetT0++;
                            offsetT1++;
                        }

                        outputOffset += output.Length * sizeof(ushort);

                        break;
                    }

                    case ExrPixelType.Float:
                    {
                        int offsetT0 = lastIn;
                        lastIn += this.Width;
                        int offsetT1 = lastIn;
                        lastIn += this.Width;
                        int offsetT2 = lastIn;
                        lastIn += this.Width;

                        Span<uint> output = MemoryMarshal.Cast<byte, uint>(buffer.Slice(outputOffset, this.Width * sizeof(uint)));
                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint t2 = uncompressed[offsetT2];
                            uint diff = (t0 << 24) | (t1 << 16) | (t2 << 8);

                            pixel += diff;
                            output[x] = pixel;

                            offsetT0++;
                            offsetT1++;
                            offsetT2++;
                        }

                        outputOffset += output.Length * sizeof(uint);

                        break;
                    }
                }
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.tmpBuffer.Dispose();
        base.Dispose(disposing);
    }
}
