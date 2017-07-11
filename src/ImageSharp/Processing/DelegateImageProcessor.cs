// <copyright file="DelegateImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    using System;
    using System.Threading.Tasks;

    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DelegateImageProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Action<Image<TPixel>> action;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="action">The action.</param>
        public DelegateImageProcessor(Action<Image<TPixel>> action)
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
            this.action?.Invoke((Image<TPixel>)source);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            // no op, we did all we wanted to do inside BeforeImageApply
        }
    }
}