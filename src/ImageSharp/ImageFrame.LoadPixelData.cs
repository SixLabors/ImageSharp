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
    internal static class ImageFrame
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
        public static ImageFrame<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<byte> data, int width, int height)
            where TPixel : struct, IPixel<TPixel>
            => LoadPixelData(configuration, MemoryMarshal.Cast<byte, TPixel>(data), width, height);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the raw <typeparamref name="TPixel"/> data.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="data">The Span containing the image Pixel data.</param>
        /// <param name="width">The width of the final image.</param>
        /// <param name="height">The height of the final image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static ImageFrame<TPixel> LoadPixelData<TPixel>(Configuration configuration, ReadOnlySpan<TPixel> data, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            int count = width * height;
            Guard.MustBeGreaterThanOrEqualTo(data.Length, count, nameof(data));

            var image = new ImageFrame<TPixel>(configuration, width, height);

            data.Slice(0, count).CopyTo(image.GetPixelSpan());

            return image;
        }
    }
}