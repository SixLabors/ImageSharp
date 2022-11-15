// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

/// <summary>
/// A Png encoder that uses the ImageSharp core encoder but the default configuration.
/// This allows encoding under environments with restricted memory.
/// </summary>
public sealed class ImageSharpPngEncoderWithDefaultConfiguration : PngEncoder
{
    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        Configuration configuration = Configuration.Default;
        MemoryAllocator allocator = configuration.MemoryAllocator;

        using PngEncoderCore encoder = new(allocator, configuration, this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
