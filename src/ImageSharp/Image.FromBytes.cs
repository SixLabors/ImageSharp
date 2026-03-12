// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Adds static methods allowing the creation of new image from a byte span.
/// </content>
public abstract partial class Image
{
    /// <summary>
    /// By reading the header on the provided byte span this calculates the images format.
    /// </summary>
    /// <param name="buffer">The byte span containing encoded image data to read the header from.</param>
    /// <returns>The <see cref="IImageFormat"/>.</returns>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static IImageFormat DetectFormat(ReadOnlySpan<byte> buffer)
        => DetectFormat(DecoderOptions.Default, buffer);

    /// <summary>
    /// By reading the header on the provided byte span this calculates the images format.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="buffer">The byte span containing encoded image data to read the header from.</param>
    /// <returns>The <see cref="IImageFormat"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static unsafe IImageFormat DetectFormat(DecoderOptions options, ReadOnlySpan<byte> buffer)
    {
        Guard.NotNull(options, nameof(options));

        if (buffer.IsEmpty)
        {
            throw new UnknownImageFormatException("Cannot detect image format from empty data.");
        }

        fixed (byte* ptr = buffer)
        {
            using UnmanagedMemoryStream stream = new(ptr, buffer.Length);
            return DetectFormat(options, stream);
        }
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="buffer">The byte array containing encoded image data to read the header from.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static ImageInfo Identify(ReadOnlySpan<byte> buffer)
        => Identify(DecoderOptions.Default, buffer);

    /// <summary>
    /// Reads the raw image information from the specified span of bytes without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="buffer">The byte span containing encoded image data to read the header from.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static unsafe ImageInfo Identify(DecoderOptions options, ReadOnlySpan<byte> buffer)
    {
        Guard.NotNull(options, nameof(options));

        if (buffer.IsEmpty)
        {
            throw new UnknownImageFormatException("Cannot identify image format from empty data.");
        }

        fixed (byte* ptr = buffer)
        {
            using UnmanagedMemoryStream stream = new(ptr, buffer.Length);
            return Identify(options, stream);
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given byte span.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="buffer">The byte span containing encoded image data.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    /// <returns>The <see cref="Image"/>.</returns>
    public static Image Load(ReadOnlySpan<byte> buffer)
        => Load(DecoderOptions.Default, buffer);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given byte span.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="buffer">The byte span containing encoded image data.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static unsafe Image Load(DecoderOptions options, ReadOnlySpan<byte> buffer)
    {
        Guard.NotNull(options, nameof(options));

        if (buffer.IsEmpty)
        {
            throw new UnknownImageFormatException("Cannot load image from empty data.");
        }

        fixed (byte* ptr = buffer)
        {
            using UnmanagedMemoryStream stream = new(ptr, buffer.Length);
            return Load(options, stream);
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given byte span.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="data">The byte span containing encoded image data.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, data);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given byte span.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="data">The byte span containing encoded image data.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="NotSupportedException">The image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static unsafe Image<TPixel> Load<TPixel>(DecoderOptions options, ReadOnlySpan<byte> data)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));

        if (data.IsEmpty)
        {
            throw new UnknownImageFormatException("Cannot load image from empty data.");
        }

        fixed (byte* ptr = data)
        {
            using UnmanagedMemoryStream stream = new(ptr, data.Length);
            return Load<TPixel>(options, stream);
        }
    }
}
