// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Decodes WebP strips and tiles directly into the requested TIFF pixel format.
/// </summary>
internal sealed class WebpTiffCompression : TiffBaseDecompressor
{
    private readonly DecoderOptions options;

    private readonly ImageFrameMetadata metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpTiffCompression"/> class.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The encoded strip or tile width.</param>
    /// <param name="bitsPerPixel">The total number of bits in each TIFF source pixel.</param>
    /// <param name="metadata">The containing TIFF frame's ICC profile metadata.</param>
    public WebpTiffCompression(DecoderOptions options, MemoryAllocator memoryAllocator, int width, int bitsPerPixel, ImageFrameMetadata metadata)
        : base(memoryAllocator, width, bitsPerPixel, TiffPredictor.None)
    {
        this.options = options;
        this.metadata = metadata;
    }

    /// <inheritdoc/>
    public override bool DecodesPixels => true;

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        // The IFD supplies the profile; WebP owns source sample interpretation and packs its result as the requested TPixel.
        using WebpDecoderCore decoder = new(new WebpDecoderOptions(this.options), this.metadata.IccProfile);

        using Image<TPixel> image = decoder.Decode<TPixel>(this.options.Configuration, stream, cancellationToken);

        this.metadata.IccProfile ??= image.Metadata.IccProfile;
        this.CopyPixels(image.Frames.RootFrame.PixelBuffer, stripHeight, pixels);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
