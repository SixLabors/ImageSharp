// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Formats.Webp.Lossy;

namespace SixLabors.ImageSharp.Formats.Webp;

internal class WebpImageInfo : IDisposable
{
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

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Vp8BitReader?.Dispose();
        this.Vp8LBitReader?.Dispose();
    }
}
