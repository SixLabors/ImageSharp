// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
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
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from raw <typeparamref name="TPixel"/> data
    /// using <paramref name="rowStride"/> pixels between source row starts.
    /// </summary>
    /// <param name="data">The readonly span containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStride">The number of pixels between row starts in <paramref name="data"/>.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive,
    /// or <paramref name="rowStride"/> is less than <paramref name="width"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> is smaller than <c>((height - 1) * rowStride) + width</c>.
    /// </exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<TPixel> data, int width, int height, int rowStride)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData(Configuration.Default, data, width, height, rowStride);

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
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from a readonly span of bytes in
    /// <typeparamref name="TPixel"/> format using <paramref name="rowStrideInBytes"/> bytes between source row starts.
    /// </summary>
    /// <param name="data">The readonly span containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStrideInBytes">The number of bytes between row starts in <paramref name="data"/>.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive,
    /// or <paramref name="rowStrideInBytes"/> resolves to fewer than <paramref name="width"/> pixels.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="rowStrideInBytes"/> is not divisible by the pixel size,
    /// or <paramref name="data"/> is smaller than the required strided image length.
    /// </exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<byte> data, int width, int height, int rowStrideInBytes)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadPixelData<TPixel>(Configuration.Default, data, width, height, rowStrideInBytes);

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
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from a readonly span of bytes in
    /// <typeparamref name="TPixel"/> format using <paramref name="rowStrideInBytes"/> bytes between source row starts.
    /// </summary>
    /// <param name="configuration">The configuration for the decoder.</param>
    /// <param name="data">The readonly span containing image data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStrideInBytes">The number of bytes between row starts in <paramref name="data"/>.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive,
    /// or <paramref name="rowStrideInBytes"/> resolves to fewer than <paramref name="width"/> pixels.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="rowStrideInBytes"/> is not divisible by the pixel size,
    /// or <paramref name="data"/> is smaller than the required strided image length.
    /// </exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(
        Configuration configuration,
        ReadOnlySpan<byte> data,
        int width,
        int height,
        int rowStrideInBytes)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));

        int rowStride = GetPixelRowStrideFromByteStride<TPixel>(width, rowStrideInBytes, nameof(rowStrideInBytes));
        return LoadPixelData(configuration, MemoryMarshal.Cast<byte, TPixel>(data), width, height, rowStride);
    }

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
        => LoadPixelData(configuration, data, width, height, width);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from raw <typeparamref name="TPixel"/> data
    /// using <paramref name="rowStride"/> pixels between source row starts.
    /// </summary>
    /// <param name="configuration">The configuration for the decoder.</param>
    /// <param name="data">The readonly span containing the image pixel data.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="rowStride">The number of pixels between row starts in <paramref name="data"/>.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is not positive,
    /// or <paramref name="rowStride"/> is less than <paramref name="width"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> is smaller than <c>((height - 1) * rowStride) + width</c>.
    /// </exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> LoadPixelData<TPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> data,
        int width,
        int height,
        int rowStride)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        ValidateWrapMemoryStride(width, height, rowStride, nameof(rowStride));
        long requiredLength = GetRequiredLength(width, height, rowStride);
        Guard.MustBeGreaterThanOrEqualTo(data.Length, requiredLength, nameof(data));

        Image<TPixel> image = new(configuration, width, height);
        image.Frames.RootFrame.PixelBuffer.CopyFrom(data, rowStride);

        return image;
    }
}
