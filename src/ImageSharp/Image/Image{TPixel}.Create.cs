// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new images from given dimensions.
    /// </content>
    public partial class Image<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class with the given height and the width.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/>.
        /// </returns>
        internal static Image<TPixel> Create(int width, int height, Configuration configuration)
        {
            return Create(width, height, null, configuration);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class with the given height and the width.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images matadata to preload.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/>.
        /// </returns>
        internal static Image<TPixel> Create(int width, int height, ImageMetaData metadata, Configuration configuration)
        {
            return new Image<TPixel>(width, height, metadata, configuration);
        }
    }
}