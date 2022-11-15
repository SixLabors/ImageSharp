// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

/// <summary>
/// A Png encoder that uses the ImageSharp core encoder but the default configuration.
/// This allows encoding under environments with restricted memory.
/// </summary>
public sealed class ImageSharpPngEncoderWithDefaultConfiguration : PngEncoder
{
    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        Configuration configuration = Configuration.Default;
        MemoryAllocator allocator = configuration.MemoryAllocator;

        using PngEncoderCore encoder = new(allocator, configuration, this);
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
    public override async Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        Configuration configuration = Configuration.Default;
        MemoryAllocator allocator = configuration.MemoryAllocator;

        // The introduction of a local variable that refers to an object the implements
        // IDisposable means you must use async/await, where the compiler generates the
        // state machine and a continuation.
        using PngEncoderCore encoder = new(allocator, configuration, this);
        await encoder.EncodeAsync(image, stream, cancellationToken);
    }
}
