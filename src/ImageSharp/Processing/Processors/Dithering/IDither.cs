// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Defines the contract for types that apply dithering to images.
    /// </summary>
    public interface IDither
    {
        /// <summary>
        /// Gets the <see cref="DitherTransformColorBehavior"/> which determines whether the
        /// transformed color should be calculated and supplied to the algorithm.
        /// </summary>
        public DitherTransformColorBehavior TransformColorBehavior { get; }

        /// <summary>
        /// Transforms the image applying a dither matrix.
        /// When <see cref="TransformColorBehavior"/> is <see cref="DitherTransformColorBehavior.PreOperation"/> this
        /// this method is destructive and will alter the input pixels.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="bounds">The region of interest bounds.</param>
        /// <param name="source">The source pixel</param>
        /// <param name="transformed">The transformed pixel</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <param name="bitDepth">The bit depth of the target palette.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The dithered result for the source pixel.</returns>
        TPixel Dither<TPixel>(
            ImageFrame<TPixel> image,
            Rectangle bounds,
            TPixel source,
            TPixel transformed,
            int x,
            int y,
            int bitDepth)
            where TPixel : struct, IPixel<TPixel>;
    }
}
