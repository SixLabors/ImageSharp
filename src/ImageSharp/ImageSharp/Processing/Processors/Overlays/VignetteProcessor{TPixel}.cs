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
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial vignette effect to an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class VignetteProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly PixelBlender<TPixel> blender;

        private readonly VignetteProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="VignetteProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public VignetteProcessor(Configuration configuration, VignetteProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(definition.GraphicsOptions);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            int startY = this.SourceRectangle.Y;
            int endY = this.SourceRectangle.Bottom;
            int startX = this.SourceRectangle.X;
            int endX = this.SourceRectangle.Right;
            TPixel vignetteColor = this.definition.VignetteColor.ToPixel<TPixel>();
            Vector2 centre = Rectangle.Center(this.SourceRectangle);

            Size sourceSize = source.Size();
            float finalRadiusX = this.definition.RadiusX.Calculate(sourceSize);
            float finalRadiusY = this.definition.RadiusY.Calculate(sourceSize);
            float rX = finalRadiusX > 0
                           ? MathF.Min(finalRadiusX, this.SourceRectangle.Width * .5F)
                           : this.SourceRectangle.Width * .5F;
            float rY = finalRadiusY > 0
                           ? MathF.Min(finalRadiusY, this.SourceRectangle.Height * .5F)
                           : this.SourceRectangle.Height * .5F;
            float maxDistance = MathF.Sqrt((rX * rX) + (rY * rY));

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
                rowColors.GetSpan().Fill(vignetteColor);

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
                                    float distance = Vector2.Distance(centre, new Vector2(i + offsetX, offsetY));
                                    amountsSpan[i] = (blendPercentage * (.9F * (distance / maxDistance))).Clamp(0, 1);
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
