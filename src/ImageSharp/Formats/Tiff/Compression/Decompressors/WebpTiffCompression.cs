// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed as a webp stream.
/// </summary>
internal class WebpTiffCompression : TiffBaseDecompressor
{
    private readonly DecoderOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpTiffCompression"/> class.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="predictor">The predictor.</param>
    public WebpTiffCompression(DecoderOptions options, MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
        => this.options = options;

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        using WebpDecoderCore decoder = new(new WebpDecoderOptions { GeneralOptions = this.options });
        using Image<Rgb24> image = decoder.Decode<Rgb24>(this.options.Configuration, stream, cancellationToken);
        CopyImageBytesToBuffer(buffer, image.Frames.RootFrame.PixelBuffer);
    }

    private static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<Rgb24> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<Rgb24> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
            rgbBytes.CopyTo(buffer[offset..]);
            offset += rgbBytes.Length;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
