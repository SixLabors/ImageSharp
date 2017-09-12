// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Defines a sampler that detects edges within an image using a single two dimensional matrix.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class EdgeDetectorProcessor<TPixel> : ImageProcessor<TPixel>, IEdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="kernelXY">The 2d gradient operator.</param>
        protected EdgeDetectorProcessor(Fast2DArray<float> kernelXY)
        {
            this.KernelXY = kernelXY;
        }

        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelXY { get; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TPixel>().Apply(source, sourceRectangle, configuration);
            }
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new ConvolutionProcessor<TPixel>(this.KernelXY).Apply(source, sourceRectangle, configuration);
        }
    }
}