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

    /// <summary>
    /// The number of bytes in each sample of the last channel of a row. A pixel is complete when this sample is complete.
    /// </summary>
    private readonly int lastChannelBytesPerSample;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunLengthExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The width of one row in pixels.</param>
    /// <param name="lastChannelBytesPerSample">The number of bytes in each sample of the last channel of a row.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public RunLengthExrCompression(
        MemoryAllocator allocator,
        uint bytesPerBlock,
        uint bytesPerRow,
        int width,
        int lastChannelBytesPerSample,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerRow, width, integrityHandling)
    {
        this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.lastChannelBytesPerSample = lastChannelBytesPerSample;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The chunk header check proves that all <paramref name="compressedBytes"/> bytes are in the stream. Thus each read in this method gets all its bytes.
    /// </remarks>
    protected override bool DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan()[..(int)uncompressedBytes];
        int written = 0;
        while (compressedBytes > 0 && written < uncompressed.Length)
        {
            int run = (sbyte)stream.ReadByte();
            compressedBytes--;
            if (run < 0)
            {
                int count = -run;

                // Read only bytes inside this block and keep the part of the literal run that fits.
                int available = (int)Math.Min(compressedBytes, (uint)Math.Min(count, uncompressed.Length - written));
                stream.ReadExactly(uncompressed.Slice(written, available));
                written += available;
                if (available != count)
                {
                    break;
                }

                compressedBytes -= (uint)available;
            }
            else
            {
                if (compressedBytes == 0)
                {
                    break;
                }

                byte value = (byte)stream.ReadByte();
                int count = Math.Min(run + 1, uncompressed.Length - written);
                uncompressed.Slice(written, count).Fill(value);
                written += count;
                if (count != run + 1)
                {
                    break;
                }

                compressedBytes--;
            }
        }

        if (written != uncompressed.Length || compressedBytes != 0)
        {
            // In the IgnoreImageData mode, the code below keeps the complete samples of the block.
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("The RLE EXR block has an invalid decompressed length.");
        }

        if (written == uncompressed.Length)
        {
            Reconstruct(uncompressed, uncompressedBytes);
            Interleave(uncompressed, uncompressedBytes, buffer);
            return true;
        }

        // EXR stores the even bytes first, then the odd bytes. A complete byte pair needs bytes from both halves.
        int halfLength = uncompressed.Length / 2;
        int completeBytes = Math.Max(0, written - halfLength) * 2;

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

        // Clear the output from the first incomplete byte pair to the end of the current block.
        buffer[completeBytes..uncompressed.Length].Clear();

        // A row holds the samples of each channel in turn. In each row, the samples of the other channels come before the samples of the last channel. Thus a
        // pixel is complete when its sample of the last channel is complete. The complete pixels are the complete rows, then the first pixels of the next row.
        int bytesPerRow = (int)this.BytesPerRow;
        int lastChannelStart = bytesPerRow - (this.Width * this.lastChannelBytesPerSample);
        int partialRowPixels = Math.Max(0, (completeBytes % bytesPerRow) - lastChannelStart) / this.lastChannelBytesPerSample;
        this.SetIncompleteBlock(completeBytes / bytesPerRow, 1, partialRowPixels);
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.tmpBuffer.Dispose();
        base.Dispose(disposing);
    }
}
