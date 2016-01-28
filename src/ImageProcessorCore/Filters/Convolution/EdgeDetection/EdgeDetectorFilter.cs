// <copyright file="EdgeDetectorFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    /// <summary>
    /// Defines a filter that detects edges within an image using a single
    /// two dimensional matrix.
    /// </summary>
    public abstract class EdgeDetectorFilter : ConvolutionFilter, IEdgeDetectorFilter
    {
        /// <inheritdoc/>
        public bool Greyscale { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Greyscale)
            {
                new GreyscaleBt709().Apply(source, source, sourceRectangle);
            }
        }
    }
}
