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
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="EntropyCropProcessor"/>.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public EntropyCropProcessor(Configuration configuration, EntropyCropProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            Rectangle rectangle;

            // TODO: This is clunky. We should add behavior enum to ExtractFrame.
            // All frames have be the same size so we only need to calculate the correct dimensions for the first frame
            using (var temp = new Image<TPixel>(this.Configuration, this.Source.Metadata.DeepClone(), new[] { this.Source.Frames.RootFrame.Clone() }))
            {
                Configuration configuration = this.Source.GetConfiguration();

                // Detect the edges.
                new SobelProcessor(false).Execute(this.Configuration, temp, this.SourceRectangle);

                // Apply threshold binarization filter.
                new BinaryThresholdProcessor(this.definition.Threshold).Execute(this.Configuration, temp, this.SourceRectangle);

                // Search for the first white pixels
                rectangle = ImageMaths.GetFilteredBoundingRectangle(temp.Frames.RootFrame, 0);
            }

            new CropProcessor(rectangle, this.Source.Size()).Execute(this.Configuration, this.Source, this.SourceRectangle);

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            // All processing happens at the image level within BeforeImageApply();
        }
    }
}
