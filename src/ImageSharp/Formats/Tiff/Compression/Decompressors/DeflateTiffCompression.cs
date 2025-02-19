// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

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
    public DeflateTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffColorType colorType, TiffPredictor predictor, bool isBigEndian, bool isTiled, int tileWidth)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
        this.colorType = colorType;
        this.isBigEndian = isBigEndian;
        this.isTiled = isTiled;
        this.tileWidth = tileWidth;
    }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        long pos = stream.Position;
        using (var deframeStream = new ZlibInflateStream(
            stream,
            () =>
            {
                int left = (int)(byteCount - (stream.Position - pos));
                return left > 0 ? left : 0;
            }))
        {
            if (deframeStream.AllocateNewBytes(byteCount, true))
            {
                DeflateStream? dataStream = deframeStream.CompressedStream;

                int totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    int bytesRead = dataStream.Read(buffer, totalRead, buffer.Length - totalRead);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }
            }
        }

        // When the image is tiled, undoing the horizontal predictor will be done for each tile row in the DecodeTilesChunky() method.
        if (this.Predictor == TiffPredictor.Horizontal)
        {
            if (this.isTiled)
            {
                HorizontalPredictor.UndoTile(buffer, this.tileWidth, this.colorType, this.isBigEndian);
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
