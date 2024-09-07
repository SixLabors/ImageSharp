// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using Modified Huffman Compression.
/// </summary>
internal sealed class ModifiedHuffmanTiffCompression : TiffBaseDecompressor
{
    private readonly byte whiteValue;

    private readonly byte blackValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModifiedHuffmanTiffCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="fillOrder">The logical order of bits within a byte.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="photometricInterpretation">The photometric interpretation.</param>
    public ModifiedHuffmanTiffCompression(MemoryAllocator allocator, TiffFillOrder fillOrder, int width, int bitsPerPixel, TiffPhotometricInterpretation photometricInterpretation)
        : base(allocator, width, bitsPerPixel)
    {
        this.FillOrder = fillOrder;
        bool isWhiteZero = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero;
        this.whiteValue = (byte)(isWhiteZero ? 0 : 1);
        this.blackValue = (byte)(isWhiteZero ? 1 : 0);
    }

    /// <summary>
    /// Gets the logical order of bits within a byte.
    /// </summary>
    private TiffFillOrder FillOrder { get; }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        ModifiedHuffmanBitReader bitReader = new(stream, this.FillOrder, byteCount);

        buffer.Clear();
        nint bitsWritten = 0;
        nuint pixelsWritten = 0;
        nint rowsWritten = 0;
        while (bitReader.HasMoreData)
        {
            bitReader.ReadNextRun();

            if (bitReader.RunLength > 0)
            {
                if (bitReader.IsWhiteRun)
                {
                    BitWriterUtils.WriteBits(buffer, bitsWritten, (int)bitReader.RunLength, this.whiteValue);
                }
                else
                {
                    BitWriterUtils.WriteBits(buffer, bitsWritten, (int)bitReader.RunLength, this.blackValue);
                }

                bitsWritten += (int)bitReader.RunLength;
                pixelsWritten += bitReader.RunLength;
            }

            if (pixelsWritten == (ulong)this.Width)
            {
                rowsWritten++;
                pixelsWritten = 0;

                // Write padding bits, if necessary.
                nint pad = 8 - Numerics.Modulo8(bitsWritten);
                if (pad != 8)
                {
                    BitWriterUtils.WriteBits(buffer, bitsWritten, pad, 0);
                    bitsWritten += pad;
                }

                if (rowsWritten >= stripHeight)
                {
                    break;
                }

                bitReader.StartNewRow();
            }

            if (pixelsWritten > (ulong)this.Width)
            {
                TiffThrowHelper.ThrowImageFormatException("ccitt compression parsing error, decoded more pixels then image width");
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
