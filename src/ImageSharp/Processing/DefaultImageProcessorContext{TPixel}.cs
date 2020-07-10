// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Performs processor application operations on the source image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal class DefaultImageProcessorContext<TPixel> : IInternalImageProcessingContext<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool mutate;
        private readonly Image<TPixel> source;
        private Image<TPixel> destination;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultImageProcessorContext{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="source">The source image.</param>
        /// <param name="mutate">Whether to mutate the image.</param>
        public DefaultImageProcessorContext(Configuration configuration, Image<TPixel> source, bool mutate)
        {
            this.Configuration = configuration;
            this.mutate = mutate;
            this.source = source;

            // Mutate acts upon the source image only.
            if (this.mutate)
            {
                this.destination = source;
            }
        }

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <inheritdoc/>
        public IDictionary<object, object> Properties { get; } = new ConcurrentDictionary<object, object>();

        /// <inheritdoc/>
        public Image<TPixel> GetResultImage()
        {
            if (!this.mutate && this.destination is null)
            {
                // Ensure we have cloned the source if we are not mutating as we might have failed
                // to register any processors.
                this.destination = this.source.Clone();
            }

            return this.destination;
        }

        /// <inheritdoc/>
        public Size GetCurrentSize() => this.GetCurrentBounds().Size;

        /// <inheritdoc/>
        public IImageProcessingContext ApplyProcessor(IImageProcessor processor)
        {
            return this.ApplyProcessor(processor, this.GetCurrentBounds());
        }

        /// <inheritdoc/>
        public IImageProcessingContext ApplyProcessor(IImageProcessor processor, Rectangle rectangle)
        {
            if (!this.mutate && this.destination is null)
            {
                // When cloning an image we can optimize the processing pipeline by avoiding an unnecessary
                // interim clone if the first processor in the pipeline is a cloning processor.
                if (processor is ICloningImageProcessor cloningImageProcessor)
                {
                    using (ICloningImageProcessor<TPixel> pixelProcessor = cloningImageProcessor.CreatePixelSpecificCloningProcessor(this.Configuration, this.source, rectangle))
                    {
                        this.destination = pixelProcessor.CloneAndExecute();
                        return this;
                    }
                }

                // Not a cloning processor? We need to create a clone to operate on.
                this.destination = this.source.Clone();
            }

            // Standard processing pipeline.
            using (IImageProcessor<TPixel> specificProcessor = processor.CreatePixelSpecificProcessor(this.Configuration, this.destination, rectangle))
            {
                specificProcessor.Execute();
            }

            return this;
        }

        private Rectangle GetCurrentBounds() => this.destination?.Bounds() ?? this.source.Bounds();
    }
}
