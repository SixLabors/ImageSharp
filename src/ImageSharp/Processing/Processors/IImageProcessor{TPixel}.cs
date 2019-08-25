// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Implements an algorithm to alter the pixels of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// The target <see cref="Image{TPixel}"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The target <see cref="Rectangle"/> doesn't fit the dimension of the image.
        /// </exception>
        void Apply();
    }
}
