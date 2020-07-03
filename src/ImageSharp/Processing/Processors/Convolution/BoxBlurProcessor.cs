// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a box blur processor of a given radius.
    /// </summary>
    public sealed class BoxBlurProcessor : IImageProcessor
    {
        /// <summary>
        /// The default radius used by the parameterless constructor.
        /// </summary>
        public const int DefaultRadius = 7;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public BoxBlurProcessor(int radius)
        {
            this.Radius = radius;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor"/> class.
        /// </summary>
        public BoxBlurProcessor()
            : this(DefaultRadius)
        {
        }

        /// <summary>
        /// Gets the Radius.
        /// </summary>
        public int Radius { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new BoxBlurProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
