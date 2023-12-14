// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Image encoder for writing an image to a stream as a Windows Cursor.
/// </summary>
public sealed class CurEncoder : QuantizingImageEncoder
{
    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        CurEncoderCore encoderCore = new();
        encoderCore.Encode(image, stream, cancellationToken);
    }
}
