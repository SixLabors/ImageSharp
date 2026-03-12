// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Image encoder for writing an image to a stream as a Windows Icon.
/// </summary>
public sealed class IcoEncoder : QuantizingImageEncoder
{
    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        IcoEncoderCore encoderCore = new(this);
        encoderCore.Encode(image, stream, cancellationToken);
    }
}
