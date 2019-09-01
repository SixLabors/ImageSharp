// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Encapsulates methods to alter the pixels of a new image, cloned from the original image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal interface ICloningImageProcessor<TPixel> : IImageProcessor<TPixel>
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
        /// <returns>Returns the cloned image after there processor has been applied to it.</returns>
        Image<TPixel> CloneAndApply();
    }
}
