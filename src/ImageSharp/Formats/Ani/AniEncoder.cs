// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Encodes images as Windows animated cursors.
/// </summary>
public sealed class AniEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AniEncoder"/> class.
    /// </summary>
    public AniEncoder()
    {
    }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        AniEncoderCore encoder = new(this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
