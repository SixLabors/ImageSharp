// <copyright file="DefaultInternalImageProcessorApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    /// <summary>
    /// The static collection of all the default image formats
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal class DefaultInternalImageProcessorApplicator<TPixel> : IInternalImageProcessorApplicator<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly bool mutate;
        private readonly Image<TPixel> source;
        private Image<TPixel> destination;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultInternalImageProcessorApplicator{TPixel}"/> class.
        /// </summary>
        /// <param name="source">The image.</param>
        /// <param name="mutate">The mutate.</param>
        public DefaultInternalImageProcessorApplicator(Image<TPixel> source, bool mutate)
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
                // ensure we have cloned it if we are not mutating as we might have failed to register any Processors
                this.destination = this.source.Clone();
            }

            return this.destination;
        }

        /// <inheritdoc/>
        public IImageProcessorApplicator<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
        {
            if (!this.mutate && this.destination == null)
            {
                // this will only work if the first processor applied is the cloning one thus
                // realistically for this optermissation to work the resize must the first processor
                // applied any only up processors will take the douple data path.
                if (processor is ICloningImageProcessor<TPixel>)
                {
                    var cloningProcessor = (ICloningImageProcessor<TPixel>)processor;
                    this.destination = cloningProcessor.CloneAndApply(this.source, rectangle);
                    return this;
                }
                else
                {
                    this.destination = this.source.Clone();
                }
            }

            processor.Apply(this.destination, rectangle);
            return this;
        }

        /// <inheritdoc/>
        public IImageProcessorApplicator<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
        {
            return this.ApplyProcessor(processor, this.source.Bounds());
        }
    }
}