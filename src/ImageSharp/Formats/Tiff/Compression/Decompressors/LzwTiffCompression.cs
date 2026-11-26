// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using LZW compression.
/// </summary>
internal sealed class LzwTiffCompression : TiffBaseDecompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LzwTiffCompression" /> class.
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
    public LzwTiffCompression(
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
        // The decoder stops before a code that is not valid, or at the end of the data. The base class reports the samples after that point.
        TiffLzwDecoder decoder = new(stream, byteCount);
        return decoder.DecodePixels(buffer);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
