// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Adds static methods allowing the creation of new image from raw pixel data.
/// </content>
public abstract partial class Image
{
    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
    /// </summary>
    /// <param name="data">The readonly span of bytes containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentException">The data length is incorrect.</exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<TPixel> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData(Configuration.Default, data, width, height);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given readonly span of bytes in <typeparamref name="TPixel"/> format.
    /// </summary>
    /// <param name="data">The readonly span of bytes containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentException">The data length is incorrect.</exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<byte> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData<TPixel>(Configuration.Default, data, width, height);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given readonly span of bytes in <typeparamref name="TPixel"/> format.
    /// </summary>
    /// <param name="configuration">The configuration for the decoder.</param>
    /// <param name="data">The readonly span of bytes containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentException">The data length is incorrect.</exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<byte> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData(configuration, MemoryMarshal.Cast<byte, TPixel>(data), width, height);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
    /// </summary>
    /// <param name="configuration">The configuration for the decoder.</param>
    /// <param name="data">The readonly span containing the image pixel data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentException">The data length is incorrect.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<TPixel> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));

        if (data.IsEmpty)
        {
            throw new ArgumentException("Pixel data cannot be empty.", nameof(data));
        }

        int count = width * height;
        Guard.MustBeGreaterThanOrEqualTo(data.Length, count, nameof(data));

        Image<TPixel> image = new(configuration, width, height);
        data = data[..count];
        data.CopyTo(image.Frames.RootFrame.PixelBuffer.FastMemoryGroup);

        return image;
    }
}
