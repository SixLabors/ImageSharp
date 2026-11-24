// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using Deflate compression.
/// </summary>
/// <remarks>
/// Note that the 'OldDeflate' compression type is identical to the 'Deflate' compression type.
/// </remarks>
internal sealed class DeflateTiffCompression : TiffBaseDecompressor
{
    private readonly bool isBigEndian;

    private readonly TiffColorType colorType;

    private readonly bool isTiled;

    private readonly int tileWidth;

    private readonly int tileHeight;

    /// <summary>
    /// The segment integrity mode of the decode. It selects what happens with damaged data.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeflateTiffCompression" /> class.
    /// </summary>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The bits used per pixel.</param>
    /// <param name="colorType">The color type of the pixel data.</param>
    /// <param name="predictor">The tiff predictor used.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    /// <param name="isTiled">Flag indicates, if the image is a tiled image.</param>
    /// <param name="tileWidth">Number of pixels in a tile row.</param>
    /// <param name="tileHeight">Number of rows in a tile.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public DeflateTiffCompression(
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        TiffColorType colorType,
        TiffPredictor predictor,
        bool isBigEndian,
        bool isTiled,
        int tileWidth,
        int tileHeight,
        SegmentIntegrityHandling integrityHandling)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
        this.integrityHandling = integrityHandling;
        this.colorType = colorType;
        this.isBigEndian = isBigEndian;
        this.isTiled = isTiled;
        this.tileWidth = tileWidth;
        this.tileHeight = tileHeight;
    }

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        long pos = stream.Position;
        int totalRead = 0;
        using (ZlibInflateReader deframeStream = new(
            stream,
            () =>
            {
                int left = (int)(byteCount - (stream.Position - pos));
                return left > 0 ? left : 0;
            }))
        {
            // The header check does not throw. A bad header gives no data, and the integrity mode below decides what
            // that does.
            if (deframeStream.AllocateNewBytes(byteCount, false))
            {
                DeflateStream? dataStream = deframeStream.CompressedStream;

                try
                {
                    while (totalRead < buffer.Length)
                    {
                        int bytesRead = dataStream.Read(buffer[totalRead..]);
                        if (bytesRead <= 0)
                        {
                            break;
                        }

                        totalRead += bytesRead;
                    }
                }
                catch (InvalidDataException ex)
                {
                    // The inflater throws for deflate data that is not valid. In the IgnoreImageData mode, the bytes
                    // before the error stay, and the code below clears the rest.
                    this.integrityHandling.ThrowOrIgnoreImageDataError("The TIFF deflate data is damaged.", ex);
                }
            }
        }

        // The buffer holds the samples of this strip or tile only. A bad header, deflate data that is not valid, or data
        // that ends early gives fewer bytes. In the IgnoreImageData mode, the strip or tile keeps the inflated bytes.
        // The buffer is reused, so the bytes after them are cleared. In the other modes, damaged data throws.
        if (totalRead < buffer.Length)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(
                "The TIFF deflate data is damaged or ends before the strip or tile is complete.");
            buffer[totalRead..].Clear();
        }

        if (this.Predictor == TiffPredictor.Horizontal)
        {
            if (this.isTiled)
            {
                // When the image is tiled, undoing the horizontal predictor will be done for each tile row.
                HorizontalPredictor.UndoTile(buffer, this.tileWidth, this.tileHeight, this.colorType, this.isBigEndian);
            }
            else
            {
                HorizontalPredictor.Undo(buffer, this.Width, this.colorType, this.isBigEndian);
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
