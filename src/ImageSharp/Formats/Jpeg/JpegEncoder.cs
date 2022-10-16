// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Encoder for writing the data image to a stream in jpeg format.
/// </summary>
public sealed class JpegEncoder : ImageEncoder
{
    /// <summary>
    /// Backing field for <see cref="Quality"/>.
    /// </summary>
    private int? quality;

    /// <summary>
    /// Gets or sets the quality, that will be used to encode the image. Quality
    /// index must be between 0 and 100 (compression from max to min).
    /// Defaults to <value>75</value>.
    /// </summary>
    /// <exception cref="ArgumentException">Quality factor must be in [1..100] range.</exception>
    public int? Quality
    {
        get => this.quality;
        set
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentException("Quality factor must be in [1..100] range.");
            }

            this.quality = value;
        }
    }

    /// <summary>
    /// Gets or sets the component encoding mode.
    /// </summary>
    /// <remarks>
    /// Interleaved encoding mode encodes all color components in a single scan.
    /// Non-interleaved encoding mode encodes each color component in a separate scan.
    /// </remarks>
    public bool? Interleaved { get; set; }

    /// <summary>
    /// Gets or sets jpeg color for encoding.
    /// </summary>
    public JpegEncodingColor? ColorType { get; set; }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        JpegEncoderCore encoder = new(this);
        encoder.Encode(image, stream);
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        JpegEncoderCore encoder = new(this);
        return encoder.EncodeAsync(image, stream, cancellationToken);
    }
}
