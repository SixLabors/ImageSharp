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
    private readonly bool preservePartialData;
    private readonly int bytesPerSample;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunLengthExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The width of one row in pixels.</param>
    /// <param name="bytesPerSample">The number of bytes in each source sample.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public RunLengthExrCompression(
        MemoryAllocator allocator,
        uint bytesPerBlock,
        uint bytesPerRow,
        uint rowsPerBlock,
        int width,
        int bytesPerSample,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
        this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.bytesPerSample = bytesPerSample;
        this.preservePartialData = integrityHandling == SegmentIntegrityHandling.IgnoreImageData;
    }

    /// <inheritdoc/>
    protected override void DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan()[..(int)uncompressedBytes];
        int written = 0;
        while (compressedBytes > 0 && written < uncompressed.Length)
        {
            int header = stream.ReadByte();
            if (header < 0)
            {
                break;
            }

            compressedBytes--;
            int run = (sbyte)header;
            if (run < 0)
            {
                int count = -run;

                // Read only bytes inside this block and keep the part of the literal run that fits.
                int available = (int)Math.Min(compressedBytes, (uint)Math.Min(count, uncompressed.Length - written));
                int read = stream.ReadAtLeast(uncompressed.Slice(written, available), available, throwOnEndOfStream: false);
                written += read;
                if (read != count)
                {
                    break;
                }

                compressedBytes -= (uint)read;
            }
            else
            {
                if (compressedBytes == 0)
                {
                    break;
                }

                int value = stream.ReadByte();
                if (value < 0)
                {
                    break;
                }

                int count = Math.Min(run + 1, uncompressed.Length - written);
                uncompressed.Slice(written, count).Fill((byte)value);
                written += count;
                if (count != run + 1)
                {
                    break;
                }

                compressedBytes--;
            }
        }

        if (!this.preservePartialData && (written != uncompressed.Length || compressedBytes != 0))
        {
            ExrThrowHelper.ThrowInvalidImageContentException("The RLE EXR block has an invalid decompressed length.");
        }

        if (written == uncompressed.Length)
        {
            Reconstruct(uncompressed, uncompressedBytes);
            Interleave(uncompressed, uncompressedBytes, buffer);
            return;
        }

        // EXR stores the even bytes first, then the odd bytes. A complete sample needs bytes from both halves.
        int halfLength = uncompressed.Length / 2;
        int completeBytes = Math.Max(0, written - halfLength) * 2;
        completeBytes -= completeBytes % this.bytesPerSample;

        if (completeBytes > 0)
        {
            // The predictor runs across both halves, so reconstruct the available prefix before copying samples.
            Reconstruct(uncompressed, (uint)written);
            for (int i = 0; i < completeBytes / 2; i++)
            {
                buffer[2 * i] = uncompressed[i];
                buffer[(2 * i) + 1] = uncompressed[halfLength + i];
            }
        }

        // Clear the output from the first incomplete sample to the end of the current block.
        buffer[completeBytes..uncompressed.Length].Clear();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
