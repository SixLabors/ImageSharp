// <copyright file="EdgeDetector2DFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Defines a filter that detects edges within an image using two
    /// one-dimensional matrices.
    /// </summary>
    public abstract class EdgeDetector2DFilter : Convolution2DFilter, IEdgeDetectorFilter
    {
        /// <inheritdoc/>
        public bool Greyscale { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Greyscale)
            {
                new GreyscaleBt709Processor().Apply(source, source, sourceRectangle);
            }
        }
    }
}
