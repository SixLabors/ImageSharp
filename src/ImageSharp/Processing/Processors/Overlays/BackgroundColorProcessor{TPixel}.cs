// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BackgroundColorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly BackgroundColorProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BackgroundColorProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BackgroundColorProcessor(Configuration configuration, BackgroundColorProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
            => this.definition = definition;

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            TPixel color = this.definition.Color.ToPixel<TPixel>();
            GraphicsOptions graphicsOptions = this.definition.GraphicsOptions;

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            Configuration configuration = this.Configuration;
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;

            using IMemoryOwner<TPixel> colors = memoryAllocator.Allocate<TPixel>(interest.Width);
            using IMemoryOwner<float> amount = memoryAllocator.Allocate<float>(interest.Width);

            colors.GetSpan().Fill(color);
            amount.GetSpan().Fill(graphicsOptions.BlendPercentage);

            PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(graphicsOptions);

            var operation = new RowOperation(configuration, interest, blender, amount, colors, source);
            ParallelRowIterator.IterateRows(
                configuration,
                interest,
                in operation);
        }

        private readonly struct RowOperation : IRowOperation
        {
            private readonly Configuration configuration;
            private readonly Rectangle bounds;
            private readonly PixelBlender<TPixel> blender;
            private readonly IMemoryOwner<float> amount;
            private readonly IMemoryOwner<TPixel> colors;
            private readonly ImageFrame<TPixel> source;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Configuration configuration,
                Rectangle bounds,
                PixelBlender<TPixel> blender,
                IMemoryOwner<float> amount,
                IMemoryOwner<TPixel> colors,
                ImageFrame<TPixel> source)
            {
                this.configuration = configuration;
                this.bounds = bounds;
                this.blender = blender;
                this.amount = amount;
                this.colors = colors;
                this.source = source;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Span<TPixel> destination =
                    this.source.GetPixelRowSpan(y)
                        .Slice(this.bounds.X, this.bounds.Width);

                // Switch color & destination in the 2nd and 3rd places because we are
                // applying the target color under the current one.
                this.blender.Blend(
                    this.configuration,
                    destination,
                    this.colors.GetSpan(),
                    destination,
                    this.amount.GetSpan());
            }
        }
    }
}
