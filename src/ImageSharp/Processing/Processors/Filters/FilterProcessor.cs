// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    public class FilterProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilterProcessor"/> class.
        /// </summary>
        /// <param name="matrix">The matrix used to apply the image filter</param>
        public FilterProcessor(ColorMatrix matrix) => this.Matrix = matrix;

        /// <summary>
        /// Gets the <see cref="ColorMatrix"/> used to apply the image filter.
        /// </summary>
        public ColorMatrix Matrix { get; }

        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new FilterProcessorImplementation<TPixel>(this);
        }
    }
}