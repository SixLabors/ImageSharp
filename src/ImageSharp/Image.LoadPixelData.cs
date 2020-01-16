// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from raw pixel data.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(TPixel[] data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData(Configuration.Default, data, width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<TPixel> data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData(Configuration.Default, data, width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(byte[] data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData<TPixel>(Configuration.Default, data, width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(ReadOnlySpan<byte> data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData<TPixel>(Configuration.Default, data, width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(Configuration config, byte[] data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData(config, MemoryMarshal.Cast<byte, TPixel>(new ReadOnlySpan<byte>(data)), width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array in <typeparamref name="TPixel"/> format.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(Configuration config, ReadOnlySpan<byte> data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData(config, MemoryMarshal.Cast<byte, TPixel>(data), width, height, rowSkip);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The Span containing the image Pixel data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(Configuration config, TPixel[] data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
        {
            return LoadPixelData(config, new ReadOnlySpan<TPixel>(data), width, height, rowSkip);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The Span containing the image Pixel data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <param name="rowSkip">Pixels to skip for each row, defaults to 0.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> LoadPixelData<TPixel>(Configuration config, ReadOnlySpan<TPixel> data, int width, int height, int rowSkip = 0)
            where TPixel : struct, IPixel<TPixel>
        {
            int rowCount = width + rowSkip;
            int count = rowCount * height;
            Guard.MustBeGreaterThanOrEqualTo(data.Length, count, nameof(data));

            var image = new Image<TPixel>(config, width, height);

            if (rowSkip == 0)
            {
                data.Slice(0, count).CopyTo(image.Frames.RootFrame.GetPixelSpan());
            }
            else
            {
                int offset = 0;
                for (int y = 0; y < height; y++)
                {
                    data.Slice(offset, width).CopyTo(image.Frames.RootFrame.GetPixelRowSpan(y));
                    offset += rowCount;
                }
            }

            return image;
        }
    }
}
