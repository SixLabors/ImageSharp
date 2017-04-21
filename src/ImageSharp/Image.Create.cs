// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    public sealed partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images matadata to preload.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/> unless <typeparamref name="TPixel"/> is <see cref="Rgba32"/> in which case it returns <see cref="Image" />
        /// </returns>
        internal static Image<TPixel> Create<TPixel>(int width, int height, ImageMetaData metadata, Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            if (typeof(TPixel) == typeof(Rgba32))
            {
                return new Image(width, height, metadata, configuration) as Image<TPixel>;
            }
            else
            {
                return new Image<TPixel>(width, height, metadata, configuration);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/> unless <typeparamref name="TPixel"/> is <see cref="Rgba32"/> in which case it returns <see cref="Image" />
        /// </returns>
        internal static Image<TPixel> Create<TPixel>(int width, int height, Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            return Image.Create<TPixel>(width, height, null, configuration);
        }
    }
}