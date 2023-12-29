// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image encoder for writing an image to a stream as HEIF images.
/// </summary>
public sealed class HeifEncoder : ImageEncoder
{
    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        HeifEncoderCore encoder = new(image.Configuration, this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
