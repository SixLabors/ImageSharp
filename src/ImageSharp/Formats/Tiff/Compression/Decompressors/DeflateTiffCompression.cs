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
        : base(memoryAllocator, width, bitsPerPixel, integrityHandling, predictor, colorType, isBigEndian, isTiled, tileWidth, tileHeight)
    {
    }

    /// <inheritdoc/>
    protected override int Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        int totalRead = 0;

        // The strip or tile is one segment of byteCount bytes. Thus no data follows it.
        using ZlibInflateReader deframeStream = new(stream);

        // The header check does not throw. A bad header gives no data. The base class reports the missing samples.
        if (deframeStream.AllocateNewBytes(byteCount))
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
            catch (InvalidDataException)
            {
                // The inflater throws for deflate data that is not valid. The inflate stops here. The bytes before the error are the decoded samples. The base
                // class reports the missing samples.
            }
        }

        return totalRead;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
