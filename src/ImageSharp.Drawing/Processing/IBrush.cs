// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Brush represents a logical configuration of a brush which can be used to source pixel colors
    /// </summary>
    /// <remarks>
    /// A brush is a simple class that will return an <see cref="BrushApplicator{TPixel}" /> that will perform the
    /// logic for retrieving pixel values for specific locations.
    /// </remarks>
    public interface IBrush
    {
        /// <summary>
        /// Creates the applicator for this brush.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="source">The source image.</param>
        /// <param name="region">The region the brush will be applied to.</param>
        /// <param name="options">The graphic options</param>
        /// <returns>
        /// The brush applicator for this brush
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would usually be the
        /// bounding box of the shape not necessarily the bounds of the whole image
        /// </remarks>
        BrushApplicator<TPixel> CreateApplicator<TPixel>(
            ImageFrame<TPixel> source,
            RectangleF region,
            GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>;
    }
}