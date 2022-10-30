// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Image encoder for writing image data to a stream in png format.
/// </summary>
public class PngEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PngEncoder"/> class.
    /// </summary>
    public PngEncoder() =>

        // We set the quantizer to null here to allow the underlying encoder to create a
        // quantizer with options appropriate to the encoding bit depth.
        this.Quantizer = null;

    /// <summary>
    /// Gets the number of bits per sample or per palette index (not per pixel).
    /// Not all values are allowed for all <see cref="ColorType" /> values.
    /// </summary>
    public PngBitDepth? BitDepth { get; init; }

    /// <summary>
    /// Gets the color type.
    /// </summary>
    public PngColorType? ColorType { get; init; }

    /// <summary>
    /// Gets the filter method.
    /// </summary>
    public PngFilterMethod? FilterMethod { get; init; }

    /// <summary>
    /// Gets the compression level 1-9.
    /// <remarks>Defaults to <see cref="PngCompressionLevel.DefaultCompression" />.</remarks>
    /// </summary>
    public PngCompressionLevel CompressionLevel { get; init; } = PngCompressionLevel.DefaultCompression;

    /// <summary>
    /// Gets the threshold of characters in text metadata, when compression should be used.
    /// </summary>
    public int TextCompressionThreshold { get; init; } = 1024;

    /// <summary>
    /// Gets the gamma value, that will be written the image.
    /// </summary>
    /// <value>The gamma value of the image.</value>
    public float? Gamma { get; init; }

    /// <summary>
    /// Gets the transparency threshold.
    /// </summary>
    public byte Threshold { get; init; } = byte.MaxValue;

    /// <summary>
    /// Gets a value indicating whether this instance should write an Adam7 interlaced image.
    /// </summary>
    public PngInterlaceMode? InterlaceMethod { get; init; }

    /// <summary>
    /// Gets the chunk filter method. This allows to filter ancillary chunks.
    /// </summary>
    public PngChunkFilter? ChunkFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether fully transparent pixels that may contain R, G, B values which are not 0,
    /// should be converted to transparent black, which can yield in better compression in some cases.
    /// </summary>
    public PngTransparentColorMode TransparentColorMode { get; init; }

    /// <inheritdoc/>
    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        using PngEncoderCore encoder = new(image.GetMemoryAllocator(), image.GetConfiguration(), this);
        encoder.Encode(image, stream);
    }

    /// <inheritdoc/>
    public override async Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        // The introduction of a local variable that refers to an object the implements
        // IDisposable means you must use async/await, where the compiler generates the
        // state machine and a continuation.
        using PngEncoderCore encoder = new(image.GetMemoryAllocator(), image.GetConfiguration(), this);
        await encoder.EncodeAsync(image, stream, cancellationToken).ConfigureAwait(false);
    }
}
