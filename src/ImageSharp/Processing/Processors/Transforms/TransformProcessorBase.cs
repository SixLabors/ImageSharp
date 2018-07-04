// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The base class for all transform processors. Any processor that changes the dimensions of the image should inherit from this.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class TransformProcessorBase<TPixel> : CloningImageProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
            => TransformHelpers.UpdateDimensionalMetData(destination);
    }
}