// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Defines an algorithm to alter the pixels of an image.
    /// Non-generic <see cref="IImageProcessor"/> implementations are responsible for:
    /// 1. Encapsulating the parameters of the algorithm.
    /// 2. Creating the generic <see cref="IImageProcessor{TPixel}"/> instance to execute the algorithm.
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// Creates a pixel specific <see cref="IImageProcessor{TPixel}"/> that is capable for executing
        /// the processing algorithm on an <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <returns>The <see cref="IImageProcessor{TPixel}"/></returns>
        IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }
}