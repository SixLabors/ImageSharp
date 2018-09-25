// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BackgroundColorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="color">The <typeparamref name="TPixel"/> to set the background color to.</param>
        /// <param name="options">The options defining blending algorithm and amount.</param>
        public BackgroundColorProcessor(TPixel color, GraphicsOptions options)
        {
            this.Color = color;
            this.GraphicsOptions = options;
        }

        /// <summary>
        /// Gets the Graphics options to alter how processor is applied.
        /// </summary>
        public GraphicsOptions GraphicsOptions { get; }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public TPixel Color { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
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

            var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

            using (IMemoryOwner<TPixel> colors = source.MemoryAllocator.Allocate<TPixel>(width))
            using (IMemoryOwner<float> amount = source.MemoryAllocator.Allocate<float>(width))
            {
                // Be careful! Do not capture colorSpan & amountSpan in the lambda below!
                Span<TPixel> colorSpan = colors.GetSpan();
                Span<float> amountSpan = amount.GetSpan();

                colorSpan.Fill(this.Color);
                amountSpan.Fill(this.GraphicsOptions.BlendPercentage);

                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.GraphicsOptions);

                ParallelHelper.IterateRows(
                    workingRect,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> destination =
                                    source.GetPixelRowSpan(y - startY).Slice(minX - startX, width);

                                // This switched color & destination in the 2nd and 3rd places because we are applying the target color under the current one
                                blender.Blend(
                                    source.MemoryAllocator,
                                    destination,
                                    colors.GetSpan(),
                                    destination,
                                    amount.GetSpan());
                            }
                        });
            }
        }
    }
}