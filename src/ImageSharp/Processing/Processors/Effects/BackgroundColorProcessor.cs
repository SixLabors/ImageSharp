// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BackgroundColorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly MemoryManager memoryManager;

        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="color">The <typeparamref name="TPixel"/> to set the background color to.</param>
        /// <param name="options">The options defining blending algorithum and amount.</param>
        public BackgroundColorProcessor(MemoryManager memoryManager, TPixel color, GraphicsOptions options)
        {
            this.Value = color;
            this.memoryManager = memoryManager;
            this.options = options;
        }

        /// <summary>
        /// Gets the Graphics options to alter how processor is applied.
        /// </summary>
        public GraphicsOptions GraphicsOptions => this.options;

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public TPixel Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            int width = maxX - minX;

            using (Buffer<TPixel> colors = this.memoryManager.Allocate<TPixel>(width))
            using (Buffer<float> amount = this.memoryManager.Allocate<float>(width))
            {
                // Be careful! Do not capture colorSpan & amountSpan in the lambda below!
                Span<TPixel> colorSpan = colors.Span;
                Span<float> amountSpan = amount.Span;

                for (int i = 0; i < width; i++)
                {
                    colorSpan[i] = this.Value;
                    amountSpan[i] = this.options.BlendPercentage;
                }

                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.options.BlenderMode);
                Parallel.For(
                    minY,
                    maxY,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> destination = source.GetPixelRowSpan(y - startY).Slice(minX - startX, width);

                        // This switched color & destination in the 2nd and 3rd places because we are applying the target colour under the current one
                        blender.Blend(destination, colors.Span, destination, amount.Span);
                    });
            }
        }
    }
}