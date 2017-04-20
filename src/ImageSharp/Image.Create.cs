// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    public sealed partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TColor}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images matadata to preload.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TColor}"/> unless <typeparamref name="TColor"/> is <see cref="Rgba32"/> in which case it returns <see cref="Image" />
        /// </returns>
        internal static Image<TColor> Create<TColor>(int width, int height, ImageMetaData metadata, Configuration configuration)
            where TColor : struct, IPixel<TColor>
        {
            if (typeof(TColor) == typeof(Rgba32))
            {
                return new Image(width, height, metadata, configuration) as Image<TColor>;
            }
            else
            {
                return new Image<TColor>(width, height, metadata, configuration);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TColor}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TColor}"/> unless <typeparamref name="TColor"/> is <see cref="Rgba32"/> in which case it returns <see cref="Image" />
        /// </returns>
        internal static Image<TColor> Create<TColor>(int width, int height, Configuration configuration)
            where TColor : struct, IPixel<TColor>
        {
            return Image.Create<TColor>(width, height, null, configuration);
        }
    }
}