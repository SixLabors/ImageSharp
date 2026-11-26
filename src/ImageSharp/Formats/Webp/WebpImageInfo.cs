// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

internal class WebpImageInfo : IDisposable
{
    /// <summary>
    /// The decoder of the VP8 data. <see cref="TryReadImageHeaders"/> creates it for a lossy image.
    /// </summary>
    private WebpLossyDecoder? lossyDecoder;

    /// <summary>
    /// The VP8 frame headers. <see cref="TryReadImageHeaders"/> reads them for a lossy image.
    /// </summary>
    private Vp8Decoder? vp8Decoder;

    /// <summary>
    /// The decoder of the VP8L data. <see cref="TryReadImageHeaders"/> creates it for a lossless image.
    /// </summary>
    private WebpLosslessDecoder? losslessDecoder;

    /// <summary>
    /// The VP8L stream headers. <see cref="TryReadImageHeaders"/> reads them for a lossless image.
    /// </summary>
    private Vp8LDecoder? vp8LDecoder;

    /// <summary>
    /// Gets or sets the size of the encoded image data in bytes.
    /// </summary>
    public uint DataSize { get; set; }

    /// <summary>
    /// Gets or sets the bitmap width in pixels.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the bitmap height in pixels.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the horizontal scale.
    /// </summary>
    public sbyte XScale { get; set; }

    /// <summary>
    /// Gets or sets the vertical scale.
    /// </summary>
    public sbyte YScale { get; set; }

    /// <summary>
    /// Gets or sets the bits per pixel.
    /// </summary>
    public WebpBitsPerPixel BitsPerPixel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this image uses lossless compression.
    /// </summary>
    public bool IsLossless { get; set; }

    /// <summary>
    /// Gets or sets additional features present in a VP8X image.
    /// </summary>
    public WebpFeatures? Features { get; set; }

    /// <summary>
    /// Gets or sets the VP8 profile / version. Valid values are between 0 and 3. Default value will be the invalid value -1.
    /// </summary>
    public int Vp8Profile { get; set; } = -1;

    /// <summary>
    /// Gets or sets the VP8 frame header.
    /// </summary>
    public Vp8FrameHeader? Vp8FrameHeader { get; set; }

    /// <summary>
    /// Gets or sets the VP8L bitreader. Will be <see langword="null"/>, if its not a lossless image.
    /// </summary>
    public Vp8LBitReader? Vp8LBitReader { get; set; }

    /// <summary>
    /// Gets or sets the VP8 bitreader. Will be <see langword="null"/>, if its not a lossy image.
    /// </summary>
    public Vp8BitReader? Vp8BitReader { get; set; }

    /// <summary>
    /// Reads the headers of the VP8 or VP8L data: the VP8 frame headers, or the VP8L transforms and entropy codes.
    /// </summary>
    /// <remarks>
    /// The decoder cannot decode a pixel without these headers. Thus, as libwebp does, the decoder reads them before it creates the image or the frame.
    /// </remarks>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged headers.</param>
    /// <param name="imageExists">
    /// A value that tells if the decoder created the image before these headers. Before the image exists, the method throws for damaged headers in each mode.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the headers are valid. <see langword="false"/> if they are damaged, the image exists and the mode ignores image data errors.
    /// </returns>
    public bool TryReadImageHeaders(
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists)
    {
        if (this.IsLossless)
        {
            this.losslessDecoder = new WebpLosslessDecoder(this.Vp8LBitReader, memoryAllocator, configuration);
            this.vp8LDecoder = new Vp8LDecoder((int)this.Width, (int)this.Height);
            return this.losslessDecoder.TryReadHeaders(this.vp8LDecoder, segmentIntegrityHandling, imageExists);
        }

        this.lossyDecoder = new WebpLossyDecoder(this.Vp8BitReader, memoryAllocator, configuration);
        return this.lossyDecoder.TryReadHeaders(this, segmentIntegrityHandling, imageExists, out this.vp8Decoder);
    }

    /// <summary>
    /// Decodes the pixels of the image. The caller reads the headers with <see cref="TryReadImageHeaders"/> before it calls this method.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="pixels">The pixel buffer to write to. It has the size of the image.</param>
    /// <param name="alphaData">The ALPH chunk data of a lossy image, or <see langword="null"/> when there is no ALPH chunk.</param>
    /// <param name="colorProfileConverter">The selected source ICC converter, if available.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls damaged or truncated image data.</param>
    public void Decode<TPixel>(
        Buffer2D<TPixel> pixels,
        IMemoryOwner<byte>? alphaData,
        ColorProfileConverter? colorProfileConverter,
        SegmentIntegrityHandling segmentIntegrityHandling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.IsLossless)
        {
            this.losslessDecoder!.Decode(this.vp8LDecoder!, pixels, (int)this.Width, colorProfileConverter, segmentIntegrityHandling);
            return;
        }

        this.lossyDecoder!.Decode(this.vp8Decoder!, pixels, this, alphaData, colorProfileConverter, segmentIntegrityHandling);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.vp8Decoder?.Dispose();
        this.vp8LDecoder?.Dispose();
        this.Vp8BitReader?.Dispose();
        this.Vp8LBitReader?.Dispose();
    }
}
