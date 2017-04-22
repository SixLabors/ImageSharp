// <copyright file="ImageExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;

    using Formats;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>
        /// The <see cref="Image{TPixel}"/>.
        /// </returns>
        public static Image<TPixel> SaveAsBmp<TPixel>(this Image<TPixel> source, Stream stream)
            where TPixel : struct, IPixel<TPixel>
                        => source.Save(stream, new BmpEncoder());
    }
}
