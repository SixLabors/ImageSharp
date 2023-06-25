// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

public class QoiEncoder : ImageEncoder
{
    /// <inheritdoc />
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
