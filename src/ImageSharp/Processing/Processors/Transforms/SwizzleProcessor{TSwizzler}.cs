// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a swizzle operation on an image.
    /// </summary>
    /// <typeparam name="TSwizzler">The swizzle function type.</typeparam>
    public sealed class SwizzleProcessor<TSwizzler> : IImageProcessor
        where TSwizzler : struct, ISwizzler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwizzleProcessor{TSwizzler}"/> class.
        /// </summary>
        /// <param name="swizzler">The swizzler operation.</param>
        public SwizzleProcessor(TSwizzler swizzler)
        {
            this.Swizzler = swizzler;
        }

        /// <summary>
        /// Gets the swizzler operation.
        /// </summary>
        public TSwizzler Swizzler { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new SwizzleProcessor<TSwizzler, TPixel>(configuration, this.Swizzler, source, sourceRectangle);
    }
}
