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
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class EdgeDetectorFilter<TColor, TPacked> : ConvolutionFilter<TColor, TPacked>, IEdgeDetectorFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorFilter{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="kernelXY">The 2d gradient operator.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public EdgeDetectorFilter(float[,] kernelXY, bool grayscale)
            : base(kernelXY)
        {
            this.Grayscale = grayscale;
        }

        /// <inheritdoc/>
        public bool Grayscale { get; }

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
