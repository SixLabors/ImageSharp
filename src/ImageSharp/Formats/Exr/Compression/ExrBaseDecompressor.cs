// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// The base EXR decompressor class.
/// </summary>
internal abstract class ExrBaseDecompressor : ExrBaseCompression
{
    /// <summary>
    /// The number of rows at the start of the last block that hold only complete pixels.
    /// </summary>
    private int completeRows;

    /// <summary>
    /// The number of rows after <see cref="completeRows"/> that start with <see cref="partialRowPixels"/> complete pixels.
    /// </summary>
    private int partialRows;

    /// <summary>
    /// The number of complete pixels at the start of each partial row.
    /// </summary>
    private int partialRowPixels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrBaseDecompressor" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The number of pixels per row.</param>
    protected ExrBaseDecompressor(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
    }

    /// <summary>
    /// Reads one original pixel block stored without compression.
    /// </summary>
    /// <param name="stream">The stream positioned at the block's pixel data.</param>
    /// <param name="uncompressedBytes">The expected byte count for the current block.</param>
    /// <param name="buffer">The reusable pixel buffer with room for the complete block.</param>
    /// <remarks>
    /// OpenEXR stores the original pixels when compression does not reduce a block's size.
    /// This rule applies to every compression method, as well as files without compression.
    /// A short read reports image-data damage before partial output can reach pixel conversion.
    /// </remarks>
    protected static void ReadUncompressedBlock(BufferedReadStream stream, uint uncompressedBytes, Span<byte> buffer)
    {
        if (stream.Read(buffer[..(int)uncompressedBytes]) != uncompressedBytes)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("The EXR block ends before all pixel data is available.");
        }
    }

    /// <summary>
    /// Reads one pixel block, including raw blocks stored by compressed files.
    /// </summary>
    /// <param name="stream">The buffered stream to decompress.</param>
    /// <param name="compressedBytes">The compressed bytes.</param>
    /// <param name="uncompressedBytes">The expected byte count for the current block.</param>
    /// <param name="buffer">The buffer to write the decompressed data to.</param>
    public void Decompress(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        // A decompressor that decodes only a part of the block calls SetIncompleteBlock.
        this.completeRows = int.MaxValue;
        this.partialRows = 0;
        this.partialRowPixels = 0;

        // OpenEXR stores the original pixel bytes when compression cannot make a block smaller.
        // Equal lengths select that raw layout for every compression method. The raw bytes must
        // bypass decompression, predictors, and byte reordering.
        if (compressedBytes == uncompressedBytes)
        {
            ReadUncompressedBlock(stream, uncompressedBytes, buffer);
        }
        else
        {
            this.DecompressCore(stream, compressedBytes, uncompressedBytes, buffer);
        }
    }

    /// <summary>
    /// Decodes a block using the file's compression method after raw-block handling.
    /// </summary>
    /// <param name="stream">The stream positioned at the block's encoded data.</param>
    /// <param name="compressedBytes">The declared encoded byte count.</param>
    /// <param name="uncompressedBytes">The expected byte count for the current block.</param>
    /// <param name="buffer">The reusable destination for the decoded pixel bytes.</param>
    protected abstract void DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer);

    /// <summary>
    /// Gets the number of complete pixels at the start of a row of the last block.
    /// </summary>
    /// <remarks>
    /// Each row of a complete block holds only complete pixels. In the IgnoreImageData mode, a block that ends early
    /// has complete rows, then rows that start with complete pixels. The decoder does not convert the other pixels,
    /// so they keep the fill value.
    /// </remarks>
    /// <param name="row">The row index in the block.</param>
    /// <returns>The number of complete pixels at the start of the row.</returns>
    public int GetCompletePixelCount(int row)
    {
        if (row < this.completeRows)
        {
            return this.Width;
        }

        return row - this.completeRows < this.partialRows ? this.partialRowPixels : 0;
    }

    /// <summary>
    /// Records the complete pixels of a block that ends before the decompressor decodes all its pixels.
    /// </summary>
    /// <param name="completeRows">The number of rows at the start of the block that hold only complete pixels.</param>
    /// <param name="partialRows">The number of rows after the complete rows that start with complete pixels.</param>
    /// <param name="partialRowPixels">The number of complete pixels at the start of each partial row.</param>
    protected void SetIncompleteBlock(int completeRows, int partialRows, int partialRowPixels)
    {
        this.completeRows = completeRows;
        this.partialRows = partialRows;
        this.partialRowPixels = partialRowPixels;
    }

    /// <summary>
    /// Decompresses zip compressed data.
    /// </summary>
    /// <param name="stream">The buffered stream to decompress.</param>
    /// <param name="compressedBytes">The compressed bytes.</param>
    /// <param name="uncompressed">The buffer to write the uncompressed data to.</param>
    /// <param name="uncompressedBytes">The uncompressed bytes.</param>
    /// <returns>The total bytes read from the stream.</returns>
    protected static int UndoZipCompression(BufferedReadStream stream, uint compressedBytes, Span<byte> uncompressed, uint uncompressedBytes)
    {
        long pos = stream.Position;
        using ZlibInflateReader inflateStream = new(
                   stream,
                   () =>
                   {
                       int left = (int)(compressedBytes - (stream.Position - pos));
                       return left > 0 ? left : 0;
                   });

        // The zlib header is part of the block data. A missing, incomplete, or bad header is damaged image data, so it
        // goes through the integrity handling of the caller as InvalidImageContentException.
        if (!inflateStream.AllocateNewBytes((int)compressedBytes, false))
        {
            ExrThrowHelper.ThrowInvalidImageContentException("ZIP compressed EXR block does not start with a valid zlib header.");
        }

        using DeflateStream dataStream = inflateStream.CompressedStream;

        int totalRead = 0;
        while (totalRead < uncompressedBytes)
        {
            int bytesRead = dataStream.Read(uncompressed.Slice(totalRead, (int)uncompressedBytes - totalRead));
            if (bytesRead <= 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead != uncompressedBytes || dataStream.ReadByte() != -1)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("ZIP compressed EXR block has an invalid decompressed length.");
        }

        return totalRead;
    }

    /// <summary>
    /// Integrate over all differences to the previous value in order to
    /// reconstruct sample values.
    /// </summary>
    /// <param name="buffer">The buffer with the data.</param>
    /// <param name="unCompressedBytes">The un compressed bytes.</param>
    protected static void Reconstruct(Span<byte> buffer, uint unCompressedBytes)
    {
        int offset = 0;
        for (int i = 0; i < unCompressedBytes - 1; i++)
        {
            byte d = (byte)(buffer[offset] + (buffer[offset + 1] - 128));
            buffer[offset + 1] = d;
            offset++;
        }
    }

    /// <summary>
    /// Interleaves the input data.
    /// </summary>
    /// <param name="source">The source data.</param>
    /// <param name="unCompressedBytes">The uncompressed bytes.</param>
    /// <param name="output">The output to write to.</param>
    protected static void Interleave(ReadOnlySpan<byte> source, uint unCompressedBytes, Span<byte> output)
    {
        int sourceOffset = 0;
        int offset0 = 0;
        int offset1 = (int)((unCompressedBytes + 1) / 2);
        while (sourceOffset < unCompressedBytes)
        {
            output[sourceOffset++] = source[offset0++];
            output[sourceOffset++] = source[offset1++];
        }
    }
}
