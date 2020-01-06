// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
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
        private readonly FillProcessor definition;

        public FillProcessor(Configuration configuration, FillProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            Rectangle sourceRectangle = this.SourceRectangle;
            Configuration configuration = this.Configuration;
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

            IBrush brush = this.definition.Brush;
            GraphicsOptions options = this.definition.Options;

            // If there's no reason for blending, then avoid it.
            if (this.IsSolidBrushWithoutBlending(out SolidBrush solidBrush))
            {
                ParallelExecutionSettings parallelSettings = ParallelExecutionSettings.FromConfiguration(configuration)
                    .MultiplyMinimumPixelsPerTask(4);

                TPixel colorPixel = solidBrush.Color.ToPixel<TPixel>();

                ParallelHelper.IterateRows(
                    workingRect,
                    parallelSettings,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                source.GetPixelRowSpan(y).Slice(minX, width).Fill(colorPixel);
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
                using (BrushApplicator<TPixel> applicator = brush.CreateApplicator(
                    configuration,
                    options,
                    source,
                    sourceRectangle))
                {
                    amount.Memory.Span.Fill(1f);

                    ParallelHelper.IterateRows(
                        workingRect,
                        configuration,
                        rows =>
                            {
                                for (int y = rows.Min; y < rows.Max; y++)
                                {
                                    int offsetY = y - startY;
                                    int offsetX = minX - startX;

                                    applicator.Apply(amount.Memory.Span, offsetX, offsetY);
                                }
                            });
                }
            }
        }

        private bool IsSolidBrushWithoutBlending(out SolidBrush solidBrush)
        {
            solidBrush = this.definition.Brush as SolidBrush;

            if (solidBrush is null)
            {
                return false;
            }

            return this.definition.Options.IsOpaqueColorWithoutBlending(solidBrush.Color);
        }
    }
}
