// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using a single two dimensional matrix.
    /// </summary>
    public abstract class EdgeDetectorProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">A value indicating whether to convert the image to grayscale before performing edge detection.</param>
        protected EdgeDetectorProcessor(bool grayscale)
        {
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets a value indicating whether to convert the image to grayscale before performing edge detection.
        /// </summary>
        public bool Grayscale { get; }

        /// <inheritdoc />
        public abstract IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }
}