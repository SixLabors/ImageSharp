// <copyright file="EdgeDetectorProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    /// <summary>
    /// Defines a sampler that detects edges within an image using a single two dimensional matrix.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class EdgeDetectorProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>, IEdgeDetectorProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public abstract float[][] KernelXY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            new ConvolutionProcessor<TColor, TPacked>(this.KernelXY).Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TColor, TPacked>().Apply(source, sourceRectangle);
            }
        }
    }
}