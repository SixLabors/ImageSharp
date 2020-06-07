// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a flipping around the center point of the image.
    /// </summary>
    public sealed class FlipProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlipProcessor"/> class.
        /// </summary>
        /// <param name="flipMode">The <see cref="FlipMode"/> used to perform flipping.</param>
        public FlipProcessor(FlipMode flipMode) => this.FlipMode = flipMode;

        /// <summary>
        /// Gets the <see cref="FlipMode"/> used to perform flipping.
        /// </summary>
        public FlipMode FlipMode { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new FlipProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
