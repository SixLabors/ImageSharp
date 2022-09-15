// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all specialized image decoders.
/// </summary>
/// <typeparam name="T">The type of specialized options.</typeparam>
public interface IImageDecoderSpecialized<T> : IImageDecoder
    where T : ISpecializedDecoderOptions
{
    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image<TPixel> Decode<TPixel>(T options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image Decode(T options, Stream stream, CancellationToken cancellationToken);
}
