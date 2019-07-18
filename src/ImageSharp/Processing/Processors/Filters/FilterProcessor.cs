// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Defines a free-form color filter by a <see cref="ColorMatrix"/>.
    /// </summary>
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

        /// <inheritdoc />
        public virtual IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new FilterProcessor<TPixel>(this);
        }
    }
}