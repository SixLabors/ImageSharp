// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines cropping operation that preserves areas of highest entropy.
    /// </summary>
    public sealed class EntropyCropProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor"/> class.
        /// </summary>
        public EntropyCropProcessor()
            : this(.5F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public EntropyCropProcessor(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1F, nameof(threshold));
            this.Threshold = threshold;
        }

        /// <summary>
        /// Gets the entropy threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new EntropyCropProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
