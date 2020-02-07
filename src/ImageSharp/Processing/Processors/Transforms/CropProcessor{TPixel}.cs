// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class CropProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Rectangle cropRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="CropProcessor"/>.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public CropProcessor(Configuration configuration, CropProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
            => this.cropRectangle = definition.CropRectangle;

        /// <inheritdoc/>
        protected override Size GetTargetSize() => new Size(this.cropRectangle.Width, this.cropRectangle.Height);

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            // Handle crop dimensions identical to the original
            if (source.Width == destination.Width
                && source.Height == destination.Height
                && this.SourceRectangle == this.cropRectangle)
            {
                // the cloned will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            Rectangle bounds = this.cropRectangle;

            // Copying is cheap, we should process more pixels per task:
            ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(this.Configuration).MultiplyMinimumPixelsPerTask(4);

            ParallelRowIterator.IterateRows2(
                bounds,
                in parallelSettings,
                new RowAction(ref bounds, source, destination));
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the processor logic for <see cref="CropProcessor{T}"/>.
        /// </summary>
        private readonly struct RowAction : IRowAction
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowAction(ref Rectangle bounds, ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
            {
                this.bounds = bounds;
                this.source = source;
                this.destination = destination;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Span<TPixel> sourceRow = this.source.GetPixelRowSpan(y).Slice(this.bounds.Left);
                Span<TPixel> targetRow = this.destination.GetPixelRowSpan(y - this.bounds.Top);
                sourceRow.Slice(0, this.bounds.Width).CopyTo(targetRow);
            }
        }
    }
}
