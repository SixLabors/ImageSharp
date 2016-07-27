// <copyright file="EdgeDetectorFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Defines a filter that detects edges within an image using a single
    /// two dimensional matrix.
    /// </summary>
    public abstract class EdgeDetectorFilter<T, TP> : ConvolutionFilter<T, TP>, IEdgeDetectorFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public bool Greyscale { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Greyscale)
            {
                new GreyscaleBt709Processor<T, TP>().Apply(source, source, sourceRectangle);
            }
        }
    }
}
