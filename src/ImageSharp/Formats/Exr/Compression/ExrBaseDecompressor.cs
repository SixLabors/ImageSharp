// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// The base class of the EXR decompressors. It reads the blocks that have no compression. Thus the NONE compression uses this class directly.
/// </summary>
internal class ExrBaseDecompressor : ExrBaseCompression
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
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The number of pixels per row.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public ExrBaseDecompressor(MemoryAllocator allocator, uint bytesPerRow, int width, SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerRow, width)
        => this.IntegrityHandling = integrityHandling;

    /// <summary>
    /// Gets the segment integrity mode of the decode.
    /// </summary>
    protected SegmentIntegrityHandling IntegrityHandling { get; }

    /// <summary>
    /// Reads one pixel block, including raw blocks stored by compressed files.
    /// </summary>
    /// <param name="stream">The stream at the start of the pixel data of the block.</param>
    /// <param name="compressedBytes">The declared byte count of the block. The chunk header check proves that these bytes are in the stream.</param>
    /// <param name="uncompressedBytes">The expected byte count for the current block.</param>
    /// <param name="buffer">The buffer to write the decompressed data to.</param>
    /// <returns>
    /// <see langword="true"/> if the buffer holds the block samples. <see cref="GetCompletePixelCount(int)"/> gives the complete pixels of each row.
    /// <see langword="false"/> if the block is damaged and the decoder ignores image data errors. If the method returns <see langword="false"/>, the caller
    /// must not convert the buffer.
    /// </returns>
    public bool Decompress(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        // A decompressor that decodes only a part of the block calls SetIncompleteBlock.
        this.completeRows = int.MaxValue;
        this.partialRows = 0;
        this.partialRowPixels = 0;

        // If the compression cannot make a block smaller, OpenEXR stores the original pixel bytes. Equal lengths select this raw layout for all compression
        // methods. The decoder does not apply decompression, predictors or byte reordering to the raw bytes.
        if (compressedBytes == uncompressedBytes)
        {
            stream.ReadExactly(buffer[..(int)uncompressedBytes]);
            return true;
        }

        return this.DecompressCore(stream, compressedBytes, uncompressedBytes, buffer);
    }

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
    /// Decodes a block that is not a raw block, with the compression method of the file.
    /// </summary>
    /// <remarks>
    /// The NONE compression has no encoded form. The chunk header check proves that a NONE block has equal compressed and uncompressed sizes. Thus
    /// <see cref="Decompress"/> does not call this method for NONE.
    /// </remarks>
    /// <param name="stream">The stream at the start of the encoded data of the block.</param>
    /// <param name="compressedBytes">The declared encoded byte count.</param>
    /// <param name="uncompressedBytes">The expected byte count for the current block.</param>
    /// <param name="buffer">The reusable destination for the decoded pixel bytes.</param>
    /// <returns>
    /// <see langword="true"/> if the buffer holds the block samples. <see langword="false"/> if the block is damaged and the decoder ignores image data errors.
    /// </returns>
    protected virtual bool DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        Debug.Fail("A NONE block always has equal compressed and uncompressed sizes.");
        return false;
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
    /// <returns>
    /// <see langword="true"/> if the data inflates to exactly <paramref name="uncompressedBytes"/> bytes.
    /// <see langword="false"/> if the data is damaged and the decoder ignores image data errors.
    /// </returns>
    protected bool UndoZipCompression(BufferedReadStream stream, uint compressedBytes, Span<byte> uncompressed, uint uncompressedBytes)
    {
        long pos = stream.Position;
        using ZlibInflateReader inflateStream = new(
                   stream,
                   () =>
                   {
                       int left = (int)(compressedBytes - (stream.Position - pos));
                       return left > 0 ? left : 0;
                   });

        // The zlib header is part of the block data. A missing, incomplete, or bad header is damaged image data.
        if (!inflateStream.AllocateNewBytes((int)compressedBytes))
        {
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("ZIP compressed EXR block does not start with a valid zlib header.");
            return false;
        }

        using DeflateStream dataStream = inflateStream.CompressedStream;

        int totalRead = 0;
        bool hasExtraData;
        try
        {
            while (totalRead < uncompressedBytes)
            {
                int bytesRead = dataStream.Read(uncompressed.Slice(totalRead, (int)uncompressedBytes - totalRead));
                if (bytesRead <= 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            hasExtraData = dataStream.ReadByte() != -1;
        }
        catch (InvalidDataException ex)
        {
            // DeflateStream throws InvalidDataException for deflate data that is not valid. In the Strict and IgnoreAncillary modes, the integrity handling
            // reports it as InvalidImageContentException.
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("The ZIP compressed EXR block is damaged.", ex);
            return false;
        }

        if (totalRead != uncompressedBytes || hasExtraData)
        {
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("ZIP compressed EXR block has an invalid decompressed length.");
            return false;
        }

        return true;
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
