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
    public abstract class EdgeDetector2DFilter<T, TP> : Convolution2DFilter<T, TP>, IEdgeDetectorFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<T, TP>().Apply(source, source, sourceRectangle);
            }
        }
    }
}
