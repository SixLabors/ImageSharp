// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// <param name="borderWrapModeX">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.</param>
        /// <param name="borderWrapModeY">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.</param>
        public BoxBlurProcessor(int radius, BorderWrappingMode borderWrapModeX, BorderWrappingMode borderWrapModeY)
        {
            this.Radius = radius;
            this.BorderWrapModeX = borderWrapModeX;
            this.BorderWrapModeY = borderWrapModeY;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public BoxBlurProcessor(int radius)
            : this(radius, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat)
        {
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

        /// <summary>
        /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.
        /// </summary>
        public BorderWrappingMode BorderWrapModeX { get; }

        /// <summary>
        /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.
        /// </summary>
        public BorderWrappingMode BorderWrapModeY { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new BoxBlurProcessor<TPixel>(configuration, this, source, sourceRectangle, this.BorderWrapModeX, this.BorderWrapModeY);
    }
}
