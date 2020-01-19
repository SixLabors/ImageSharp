// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

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

            (Size size, Rectangle rectangle) = ResizeHelper.CalculateTargetLocationAndBounds(sourceSize, options);

            this.Sampler = options.Sampler;
            this.TargetWidth = size.Width;
            this.TargetHeight = size.Height;
            this.TargetRectangle = rectangle;
            this.Compand = options.Compand;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the target width.
        /// </summary>
        public int TargetWidth { get; }

        /// <summary>
        /// Gets the target height.
        /// </summary>
        public int TargetHeight { get; }

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle TargetRectangle { get; }

        /// <summary>
        /// Gets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; }

        /// <inheritdoc />
        public override ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new ResizeProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
