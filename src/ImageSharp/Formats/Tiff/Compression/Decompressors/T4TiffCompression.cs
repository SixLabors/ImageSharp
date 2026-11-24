// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using CCITT T4 compression.
/// </summary>
internal sealed class T4TiffCompression : TiffBaseDecompressor
{
    private readonly FaxCompressionOptions faxCompressionOptions;

    private readonly CcittBitOrder bitOrder;

    /// <summary>
    /// The segment integrity mode of the decode. It selects what happens with damaged data.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="T4TiffCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="faxOptions">Fax compression options.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public T4TiffCompression(
        MemoryAllocator allocator,
        CcittBitOrder bitOrder,
        int width,
        int bitsPerPixel,
        FaxCompressionOptions faxOptions,
        SegmentIntegrityHandling integrityHandling)
        : base(allocator, width, bitsPerPixel)
    {
        this.faxCompressionOptions = faxOptions;
        this.bitOrder = bitOrder;
        this.integrityHandling = integrityHandling;
    }

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        if (this.faxCompressionOptions.HasFlag(FaxCompressionOptions.TwoDimensionalCoding))
        {
            TiffThrowHelper.ThrowNotSupported("TIFF CCITT 2D compression is not yet supported");
        }

        // The sample rows hold 1 for a black pixel of the code. The TIFF color decoder applies the photometric
        // interpretation, which reverses white and black for BlackIsZero. See TIFF 6.0 specification, section 10, page 44.
        buffer.Clear();

        // Each sample row holds 1 bit for each pixel and starts on a byte boundary.
        // The decoder skips the fill bits before each EOL code, so the EolPadding option needs no other handling.
        int rowBytes = (this.Width + 7) / 8;
        T4RowDecoder decoder = new(stream, this.bitOrder, byteCount, this.Width);
        for (int y = 0; y < stripHeight; y++)
        {
            if (!decoder.DecodeRow(buffer.Slice(y * rowBytes, rowBytes)))
            {
                break;
            }
        }

        // In the IgnoreImageData mode, the strip or tile keeps the decoded rows, and the other rows stay 0. In the other
        // modes, damaged data throws.
        if (decoder.IsDamaged)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(
                "The TIFF CCITT T.4 data is damaged or ends before the strip or tile is complete.");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
