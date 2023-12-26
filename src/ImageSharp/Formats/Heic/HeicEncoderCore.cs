// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Image encoder for writing an image to a stream as a HEIC image.
/// </summary>
internal sealed class HeicEncoderCore : IImageEncoderInternals
{
    /// <summary>
    /// The global configuration.
    /// </summary>
    private Configuration configuration;

    /// <summary>
    /// The encoder with options.
    /// </summary>
    private readonly HeicEncoder encoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeicEncoderCore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="encoder">The encoder with options.</param>
    public HeicEncoderCore(Configuration configuration, HeicEncoder encoder)
    {
        this.configuration = configuration;
        this.encoder = encoder;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        // TODO: Implement
        stream.Flush();
    }
}
