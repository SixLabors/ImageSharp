// <copyright file="EdgeDetector2DProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    /// <summary>
    /// Defines a sampler that detects edges within an image using two one-dimensional matrices.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public abstract class EdgeDetector2DProcessor<TColor> : ImageProcessor<TColor>, IEdgeDetectorProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public abstract float[][] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public abstract float[][] KernelY { get; }

        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <inheritdoc />
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            new Convolution2DProcessor<TColor>(this.KernelX, this.KernelY).Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TColor>().Apply(source, sourceRectangle);
            }
        }
    }
}