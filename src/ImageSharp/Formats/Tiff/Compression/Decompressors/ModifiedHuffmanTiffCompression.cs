// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using Modified Huffman Compression.
/// </summary>
internal sealed class ModifiedHuffmanTiffCompression : TiffBaseDecompressor
{
    private readonly CcittBitOrder bitOrder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModifiedHuffmanTiffCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    public ModifiedHuffmanTiffCompression(
        MemoryAllocator allocator,
        CcittBitOrder bitOrder,
        int width,
        int bitsPerPixel)
        : base(allocator, width, bitsPerPixel)
    {
        this.bitOrder = bitOrder;
    }

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        // The sample rows hold 1 for a black pixel of the code. The TIFF color decoder applies the photometric
        // interpretation, which reverses white and black for BlackIsZero. See TIFF 6.0 specification, section 10, page 44.
        buffer.Clear();

        // Each sample row holds 1 bit for each pixel and starts on a byte boundary.
        int rowBytes = (this.Width + 7) / 8;
        ModifiedHuffmanRowDecoder decoder = new(stream, this.bitOrder, byteCount, this.Width);
        for (int y = 0; y < stripHeight; y++)
        {
            decoder.DecodeRow(buffer.Slice(y * rowBytes, rowBytes));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
