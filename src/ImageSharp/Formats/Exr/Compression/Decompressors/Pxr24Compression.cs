// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Compression.Zlib;
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

    private readonly uint rowsPerBlock;

    private readonly int channelCount;

    private readonly ExrPixelType pixelType;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pxr24Compression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per pixel row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="channelCount">The number of channels for a pixel.</param>
    /// <param name="pixelType">The pixel type.</param>
    public Pxr24Compression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width, int channelCount, ExrPixelType pixelType)
        : base(allocator, bytesPerBlock, bytesPerRow, width)
    {
        this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.rowsPerBlock = rowsPerBlock;
        this.channelCount = channelCount;
        this.pixelType = pixelType;
    }

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();
        Span<ushort> outputBufferHalf = MemoryMarshal.Cast<byte, ushort>(buffer);
        Span<uint> outputBufferFloat = MemoryMarshal.Cast<byte, uint>(buffer);
        Span<uint> outputBufferUint = MemoryMarshal.Cast<byte, uint>(buffer);

        uint uncompressedBytes = this.BytesPerBlock;
        UndoZipCompression(stream, compressedBytes, uncompressed, uncompressedBytes);

        int lastIn = 0;
        int outputOffset = 0;
        for (int y = 0; y < this.rowsPerBlock; y++)
        {
            for (int c = 0; c < this.channelCount; c++)
            {
                switch (this.pixelType)
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

                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint t2 = uncompressed[offsetT2];
                            uint t3 = uncompressed[offsetT3];
                            uint diff = (t0 << 24) | (t1 << 16) | (t2 << 8) | t3;

                            pixel += diff;
                            outputBufferUint[outputOffset] = pixel;

                            offsetT0++;
                            offsetT1++;
                            offsetT2++;
                            offsetT3++;
                            outputOffset++;
                        }

                        break;
                    }

                    case ExrPixelType.Half:
                    {
                        int offsetT0 = lastIn;
                        lastIn += this.Width;
                        int offsetT1 = lastIn;
                        lastIn += this.Width;

                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint diff = (t0 << 8) | t1;

                            pixel += diff;
                            outputBufferHalf[outputOffset] = (ushort)pixel;

                            offsetT0++;
                            offsetT1++;
                            outputOffset++;
                        }

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

                        uint pixel = 0;
                        for (int x = 0; x < this.Width; x++)
                        {
                            uint t0 = uncompressed[offsetT0];
                            uint t1 = uncompressed[offsetT1];
                            uint t2 = uncompressed[offsetT2];
                            uint diff = (t0 << 24) | (t1 << 16) | (t2 << 8);

                            pixel += diff;
                            outputBufferFloat[outputOffset] = pixel;

                            offsetT0++;
                            offsetT1++;
                            offsetT2++;
                            outputOffset++;
                        }

                        break;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
