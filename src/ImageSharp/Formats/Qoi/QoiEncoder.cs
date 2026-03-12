// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Image encoder for writing an image to a stream as a QOI image
/// </summary>
public class QoiEncoder : AlphaAwareImageEncoder
{
    /// <summary>
    /// Gets the color channels on the image that can be
    /// RGB or RGBA. This is purely informative. It doesn't
    /// change the way data chunks are encoded.
    /// </summary>
    public QoiChannels? Channels { get; init; }

    /// <summary>
    /// Gets the color space of the image that can be sRGB with
    /// linear alpha or all channels linear. This is purely
    /// informative. It doesn't change the way data chunks are encoded.
    /// </summary>
    public QoiColorSpace? ColorSpace { get; init; }

    /// <inheritdoc />
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        QoiEncoderCore encoder = new(this, image.Configuration);
        encoder.Encode(image, stream, cancellationToken);
    }
}
