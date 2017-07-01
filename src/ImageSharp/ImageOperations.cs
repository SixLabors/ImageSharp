// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Collections.Generic;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    /// <summary>
    /// The static collection of all the default image formats
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal class ImageOperations<TPixel> : IImageOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Image<TPixel> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageOperations{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image.</param>
        public ImageOperations(Image<TPixel> image)
        {
            this.image = image;
        }

        /// <inheritdoc/>
        public IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
        {
            // TODO : make this queue, and allow special processors managage the cloing operation for 'generate'
            // to allow things like resize to not need to retain an extra copy of image data in memory, and to
            // prevent an pixel copy operation
            this.image.ApplyProcessor(processor, rectangle);
            return this;
        }

        /// <inheritdoc/>
        public IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
        {
            return this.ApplyProcessor(processor, this.image.Bounds);
        }

        /// <summary>
        /// Applies a bluck colelctino of pressorce at once
        /// </summary>
        /// <param name="processors">Processors to apply</param>
        /// <returns>this </returns>
        public IImageOperations<TPixel> ApplyProcessors(IEnumerable<IImageProcessor<TPixel>> processors)
        {
            foreach (var processor in processors)
            {
                return this.ApplyProcessor(processor);
            }

            return this;
        }
    }
}