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
        private readonly EntropyCropProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="EntropyCropProcessor"/>.</param>
        public EntropyCropProcessor(EntropyCropProcessor definition)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            Rectangle rectangle;

            // All frames have be the same size so we only need to calculate the correct dimensions for the first frame
            using (ImageFrame<TPixel> temp = source.Frames.RootFrame.Clone())
            {
                Configuration configuration = source.GetConfiguration();

                // Detect the edges.
                new SobelProcessor(false).Apply(temp, sourceRectangle, configuration);

                // Apply threshold binarization filter.
                new BinaryThresholdProcessor(this.definition.Threshold).Apply(temp, sourceRectangle, configuration);

                // Search for the first white pixels
                rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);
            }

            new CropProcessor(rectangle, source.Size()).Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // All processing happens at the image level within BeforeImageApply();
        }
    }
}