// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Performs processor application operations on the source image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal class DefaultInternalImageProcessorContext<TPixel> : IInternalImageProcessingContext<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly bool mutate;
        private readonly Image<TPixel> source;
        private Image<TPixel> destination;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultInternalImageProcessorContext{TPixel}"/> class.
        /// </summary>
        /// <param name="source">The image.</param>
        /// <param name="mutate">The mutate.</param>
        public DefaultInternalImageProcessorContext(Image<TPixel> source, bool mutate)
        {
            this.mutate = mutate;
            this.source = source;
            if (this.mutate)
            {
                this.destination = source;
            }
        }

        /// <inheritdoc/>
        public Image<TPixel> Apply()
        {
            if (!this.mutate && this.destination == null)
            {
                // Ensure we have cloned it if we are not mutating as we might have failed to register any processors
                this.destination = this.source.Clone();
            }

            return this.destination;
        }

        /// <inheritdoc/>
        public IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
        {
            if (!this.mutate && this.destination == null)
            {
                // This will only work if the first processor applied is the cloning one thus
                // realistically for this optimization to work the resize must the first processor
                // applied any only up processors will take the double data path.
                if (processor is ICloningImageProcessor<TPixel> cloningImageProcessor)
                {
                    this.destination = cloningImageProcessor.CloneAndApply(this.source, rectangle);
                    return this;
                }

                this.destination = this.source.Clone();
            }

            processor.Apply(this.destination, rectangle);
            return this;
        }

        /// <inheritdoc/>
        public IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
        {
            return this.ApplyProcessor(processor, this.source.Bounds());
        }
    }
}