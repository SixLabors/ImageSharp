// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Encapsulates properties and methods required to perform ordered dithering on an image.
    /// </summary>
    public interface IOrderedDither
    {
        /// <summary>
        /// Transforms the image applying the dither matrix. This method alters the input pixels array
        /// </summary>
        /// <param name="image">The image</param>
        /// <param name="source">The source pixel</param>
        /// <param name="upper">The color to apply to the pixels above the threshold.</param>
        /// <param name="lower">The color to apply to the pixels below the threshold.</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel upper, TPixel lower, float threshold, int x, int y)
            where TPixel : struct, IPixel<TPixel>;
    }
}