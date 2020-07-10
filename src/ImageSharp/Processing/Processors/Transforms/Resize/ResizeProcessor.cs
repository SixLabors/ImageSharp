// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines an image resizing operation with the given <see cref="IResampler"/> and dimensional parameters.
    /// </summary>
    public class ResizeProcessor : CloningImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor"/> class.
        /// </summary>
        /// <param name="options">The resize options.</param>
        /// <param name="sourceSize">The source image size.</param>
        public ResizeProcessor(ResizeOptions options, Size sourceSize)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(options.Sampler, nameof(options.Sampler));
            Guard.MustBeValueType(options.Sampler, nameof(options.Sampler));

            (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(sourceSize, options);

            this.Sampler = options.Sampler;
            this.DestinationWidth = size.Width;
            this.DestinationHeight = size.Height;
            this.DestinationRectangle = rectangle;
            this.Compand = options.Compand;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the destination width.
        /// </summary>
        public int DestinationWidth { get; }

        /// <summary>
        /// Gets the destination height.
        /// </summary>
        public int DestinationHeight { get; }

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle DestinationRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; }

        /// <inheritdoc />
        public override ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new ResizeProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
