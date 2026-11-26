// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using CCITT T4 compression.
/// </summary>
internal sealed class T4TiffCompression : TiffBaseDecompressor
{
    private readonly CcittBitOrder bitOrder;

    /// <summary>
    /// Initializes a new instance of the <see cref="T4TiffCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public T4TiffCompression(
        MemoryAllocator allocator,
        CcittBitOrder bitOrder,
        int width,
        int bitsPerPixel,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, width, bitsPerPixel, integrityHandling)
        => this.bitOrder = bitOrder;

    /// <inheritdoc/>
    protected override int Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        // The parser rejects 2D coding (T4Options bit 0). Thus the data has only the one-dimensional coding. The sample rows hold 1 for a black pixel of the
        // code. The TIFF color decoder applies the photometric interpretation. For BlackIsZero, this interpretation reverses white and black. See TIFF 6.0
        // specification, section 10, page 44.
        buffer.Clear();

        // Each sample row holds 1 bit for each pixel and starts on a byte boundary.
        // The decoder skips the fill bits before each EOL code, so the EolPadding option needs no other handling.
        int rowBytes = (this.Width + 7) / 8;

        // The row decoder reports damaged data where it finds it. In the IgnoreImageData mode, the strip or tile keeps the decoded rows. The other rows stay 0.
        // Thus each byte of the buffer has a defined value.
        T4RowDecoder decoder = new(stream, this.bitOrder, byteCount, this.Width, this.IntegrityHandling);
        for (int y = 0; y < stripHeight; y++)
        {
            if (!decoder.DecodeRow(buffer.Slice(y * rowBytes, rowBytes)))
            {
                break;
            }
        }

        return buffer.Length;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
