// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest entropy.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EntropyCropProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TPixel}"/> class.
        /// </summary>
        public EntropyCropProcessor()
        : this(.5F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TPixel}"/> class.
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

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            Rectangle rectangle;

            // All frames have be the same size so we only need to calculate the correct dimensions for the first frame
            using (ImageFrame<TPixel> temp = source.Frames.RootFrame.Clone())
            {
                Configuration configuration = source.GetConfiguration();

                // Detect the edges.
                new SobelProcessor<TPixel>(false).Apply(temp, sourceRectangle, configuration);

                // Apply threshold binarization filter.
                new BinaryThresholdProcessor<TPixel>(this.Threshold).Apply(temp, sourceRectangle, configuration);

                // Search for the first white pixels
                rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);
            }

            new CropProcessor<TPixel>(rectangle).Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // All processing happens at the image level within BeforeImageApply();
        }
    }
}