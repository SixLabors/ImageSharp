// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Contains methods for loading raw pixel data.
/// </content>
public partial class ImageFrame
{
    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="data">The byte array containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    internal static ImageFrame<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<byte> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData(configuration, MemoryMarshal.Cast<byte, TPixel>(data), width, height);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="data">The byte array containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStrideInBytes">The number of bytes between row starts in <paramref name="data"/>.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    internal static ImageFrame<TPixel> LoadPixelData<TPixel>(
        Configuration configuration,
        ReadOnlySpan<byte> data,
        int width,
        int height,
        int rowStrideInBytes)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int pixelSizeInBytes = Unsafe.SizeOf<TPixel>();
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(rowStrideInBytes, 0, nameof(rowStrideInBytes));
        Guard.IsTrue(
            rowStrideInBytes % pixelSizeInBytes == 0,
            nameof(rowStrideInBytes),
            "The row stride in bytes must be divisible by the pixel size.");

        int rowStride = rowStrideInBytes / pixelSizeInBytes;
        return LoadPixelData(configuration, MemoryMarshal.Cast<byte, TPixel>(data), width, height, rowStride);
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="data">The Span containing the image Pixel data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    internal static ImageFrame<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<TPixel> data, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData(configuration, data, width, height, width);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from raw <typeparamref name="TPixel"/> data
    /// using <paramref name="rowStride"/> pixels between source row starts.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="data">The span containing the image pixel data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStride">The number of pixels between row starts in <paramref name="data"/>.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    internal static ImageFrame<TPixel> LoadPixelData<TPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> data,
        int width,
        int height,
        int rowStride)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.MustBeGreaterThan(width, 0, nameof(width));
        Guard.MustBeGreaterThan(height, 0, nameof(height));
        Guard.MustBeGreaterThanOrEqualTo(rowStride, width, nameof(rowStride));

        long requiredLength = checked(((long)(height - 1) * rowStride) + width);
        Guard.MustBeGreaterThanOrEqualTo(data.Length, requiredLength, nameof(data));

        ImageFrame<TPixel> image = new(configuration, width, height);
        image.PixelBuffer.CopyFrom(data, rowStride);

        return image;
    }
}
