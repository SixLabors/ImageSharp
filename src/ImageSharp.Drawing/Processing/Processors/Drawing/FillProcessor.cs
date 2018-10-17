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

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Using the brush as a source of pixels colors blends the brush color with source.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class FillProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The brush.
        /// </summary>
        private readonly IBrush<TPixel> brush;
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="brush">The brush to source pixel colors from.</param>
        /// <param name="options">The options</param>
        public FillProcessor(IBrush<TPixel> brush, GraphicsOptions options)
        {
            this.brush = brush;
            this.options = options;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            int width = maxX - minX;

            var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

            // If there's no reason for blending, then avoid it.
            if (this.IsSolidBrushWithoutBlending(out SolidBrush<TPixel> solidBrush))
            {
                ParallelExecutionSettings parallelSettings = configuration.GetParallelSettings().MultiplyMinimumPixelsPerTask(4);

                ParallelHelper.IterateRows(
                    workingRect,
                    parallelSettings,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                source.GetPixelRowSpan(y).Slice(minX, width).Fill(solidBrush.Color);
                            }
                        });
            }
            else
            {
                // Reset offset if necessary.
                if (minX > 0)
                {
                    startX = 0;
                }

                if (minY > 0)
                {
                    startY = 0;
                }

                using (IMemoryOwner<float> amount = source.MemoryAllocator.Allocate<float>(width))
                using (BrushApplicator<TPixel> applicator = this.brush.CreateApplicator(
                    source,
                    sourceRectangle,
                    this.options))
                {
                    amount.GetSpan().Fill(1f);

                    ParallelHelper.IterateRows(
                        workingRect,
                        configuration,
                        rows =>
                            {
                                for (int y = rows.Min; y < rows.Max; y++)
                                {
                                    int offsetY = y - startY;
                                    int offsetX = minX - startX;

                                    applicator.Apply(amount.GetSpan(), offsetX, offsetY);
                                }
                            });
                }
            }
        }

        private bool IsSolidBrushWithoutBlending(out SolidBrush<TPixel> solidBrush)
        {
            solidBrush = this.brush as SolidBrush<TPixel>;

            if (solidBrush == null)
            {
                return false;
            }

            return this.options.IsOpaqueColorWithoutBlending(solidBrush.Color);
        }
    }
}