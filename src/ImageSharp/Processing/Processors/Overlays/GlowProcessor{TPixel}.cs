// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial glow effect an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GlowProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly PixelBlender<TPixel> blender;
        private readonly GlowProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlowProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="GlowProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public GlowProcessor(Configuration configuration, GlowProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(definition.GraphicsOptions);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            // TODO: can we simplify the rectangle calculation?
            int startY = this.SourceRectangle.Y;
            int endY = this.SourceRectangle.Bottom;
            int startX = this.SourceRectangle.X;
            int endX = this.SourceRectangle.Right;
            TPixel glowColor = this.definition.GlowColor.ToPixel<TPixel>();
            Vector2 center = Rectangle.Center(this.SourceRectangle);

            float finalRadius = this.definition.Radius.Calculate(source.Size());

            float maxDistance = finalRadius > 0
                                    ? MathF.Min(finalRadius, this.SourceRectangle.Width * .5F)
                                    : this.SourceRectangle.Width * .5F;

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
            int offsetX = minX - startX;

            var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

            float blendPercentage = this.definition.GraphicsOptions.BlendPercentage;

            using (IMemoryOwner<TPixel> rowColors = source.MemoryAllocator.Allocate<TPixel>(width))
            {
                rowColors.GetSpan().Fill(glowColor);

                ParallelHelper.IterateRowsWithTempBuffer<float>(
                    workingRect,
                    this.Configuration,
                    (rows, amounts) =>
                        {
                            Span<float> amountsSpan = amounts.Span;

                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                int offsetY = y - startY;

                                for (int i = 0; i < width; i++)
                                {
                                    float distance = Vector2.Distance(center, new Vector2(i + offsetX, offsetY));
                                    amountsSpan[i] =
                                        (blendPercentage * (1 - (.95F * (distance / maxDistance)))).Clamp(0, 1);
                                }

                                Span<TPixel> destination = source.GetPixelRowSpan(offsetY).Slice(offsetX, width);

                                this.blender.Blend(
                                    source.Configuration,
                                    destination,
                                    destination,
                                    rowColors.GetSpan(),
                                    amountsSpan);
                            }
                        });
            }
        }
    }
}
