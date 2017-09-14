// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EntropyCropProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public EntropyCropProcessor(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            using (ImageFrame<TPixel> temp = source.Clone())
            {
                // Detect the edges.
                new SobelProcessor<TPixel>().Apply(temp, sourceRectangle, configuration);

                // Apply threshold binarization filter.
                new BinaryThresholdProcessor<TPixel>(this.Threshold).Apply(temp, sourceRectangle, configuration);

                // Search for the first white pixels
                Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);

                if (rectangle == sourceRectangle)
                {
                    return;
                }

                new CropProcessor<TPixel>(rectangle).Apply(source, sourceRectangle, configuration);
            }
        }
    }
}