// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
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
    /// <param name="format">The format or null if none found.</param>
    /// <returns>returns true when format was detected otherwise false.</returns>
    public static bool TryDetectFormat(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out IImageFormat? format)
        => TryDetectFormat(DecoderOptions.Default, buffer, out format);

    /// <summary>
    /// By reading the header on the provided byte span this calculates the images format.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="buffer">The byte span containing encoded image data to read the header from.</param>
    /// <param name="format">The mime type or null if none found.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <returns>returns true when format was detected otherwise false.</returns>
    public static bool TryDetectFormat(DecoderOptions options, ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out IImageFormat? format)
    {
        Guard.NotNull(options, nameof(options.Configuration));

        Configuration configuration = options.Configuration;
        int maxHeaderSize = configuration.MaxHeaderSize;
        if (maxHeaderSize <= 0)
        {
            format = null;
            return false;
        }

        foreach (IImageFormatDetector detector in configuration.ImageFormatsManager.FormatDetectors)
        {
            if (detector.TryDetectFormat(buffer, out format))
            {
                return true;
            }
        }

        format = default;
        return false;
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="buffer">The byte array containing encoded image data to read the header from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="NotSupportedException">The data is not readable.</exception>
    public static bool TryIdentify(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out ImageInfo? info)
        => TryIdentify(DecoderOptions.Default, buffer, out info);

    /// <summary>
    /// Reads the raw image information from the specified span of bytes without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="buffer">The byte span containing encoded image data to read the header from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="NotSupportedException">The data is not readable.</exception>
    public static unsafe bool TryIdentify(DecoderOptions options, ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out ImageInfo? info)
    {
        fixed (byte* ptr = buffer)
        {
            using UnmanagedMemoryStream stream = new(ptr, buffer.Length);
            return TryIdentify(options, stream, out info);
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
        fixed (byte* ptr = data)
        {
            using UnmanagedMemoryStream stream = new(ptr, data.Length);
            return Load<TPixel>(options, stream);
        }
    }
}
