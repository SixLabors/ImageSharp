// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed using LZW compression.
/// </summary>
internal sealed class LzwTiffCompression : TiffBaseDecompressor
{
    private readonly bool isBigEndian;

    private readonly TiffColorType colorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzwTiffCompression" /> class.
    /// </summary>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The bits used per pixel.</param>
    /// <param name="colorType">The color type of the pixel data.</param>
    /// <param name="predictor">The tiff predictor used.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public LzwTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffColorType colorType, TiffPredictor predictor, bool isBigEndian)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
        this.colorType = colorType;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        TiffLzwDecoder decoder = new(stream);
        decoder.DecodePixels(buffer);

        if (this.Predictor == TiffPredictor.Horizontal)
        {
            HorizontalPredictor.Undo(buffer, this.Width, this.colorType, this.isBigEndian);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
