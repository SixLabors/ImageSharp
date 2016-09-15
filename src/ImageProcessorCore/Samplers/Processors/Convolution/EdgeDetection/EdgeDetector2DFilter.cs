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
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class EdgeDetector2DFilter<TColor, TPacked> : ImageSampler<TColor, TPacked>, IEdgeDetectorFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public abstract float[,] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public abstract float[,] KernelY { get; }

        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <inheritdoc />
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // TODO: Figure out a way to pass event handlers to child classes. 
            new Convolution2DFilter<TColor, TPacked>(this.KernelX, this.KernelY).Apply(target, source, targetRectangle, sourceRectangle, startY, endY);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TColor, TPacked>().Apply(source, sourceRectangle);
            }
        }
    }
}