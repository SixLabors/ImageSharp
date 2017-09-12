// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DelegateProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Action<Image<TPixel>> action;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="action">The action.</param>
        public DelegateProcessor(Action<Image<TPixel>> action)
        {
            this.action = action;
        }

        /// <summary>
        /// Gets the action that will be applied to the image.
        /// </summary>
        internal Action<Image<TPixel>> Action => this.action;

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.action?.Invoke(source);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // NOP, we did all we wanted to do inside BeforeImageApply
        }
    }
}