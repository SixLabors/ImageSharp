// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Encapsulates methods to alter the pixels of a new image, cloned from the original image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface ICloningImageProcessor<TPixel> : IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Clones the specified <see cref="Image{TPixel}"/> and executes the process against the clone.
        /// </summary>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        Image<TPixel> CloneAndExecute();
    }
}
