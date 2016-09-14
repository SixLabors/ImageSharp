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
    public class EdgeDetector2DFilter<TColor, TPacked> : ImageSampler<TColor, TPacked>, IEdgeDetectorFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DFilter{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public EdgeDetector2DFilter(float[,] kernelX, float[,] kernelY, bool grayscale)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public float[,] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public float[,] KernelY { get; }

        /// <inheritdoc/>
        public bool Grayscale { get; }

        /// <inheritdoc />
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
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