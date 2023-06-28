// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Image encoder for writing an image to a stream as a QOI image
/// </summary>
public class QoiEncoder : ImageEncoder
{
    /// <inheritdoc />
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        QoiEncoderCore encoder = new(image.GetConfiguration(), this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
