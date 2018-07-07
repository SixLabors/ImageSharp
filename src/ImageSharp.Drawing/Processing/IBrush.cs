// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Brush represents a logical configuration of a brush which can be used to source pixel colors
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <remarks>
    /// A brush is a simple class that will return an <see cref="BrushApplicator{TPixel}" /> that will perform the
    /// logic for converting a pixel location to a <typeparamref name="TPixel"/>.
    /// </remarks>
    public interface IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Creates the applicator for this brush.
        /// </summary>
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
        BrushApplicator<TPixel> CreateApplicator(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options);
    }
}