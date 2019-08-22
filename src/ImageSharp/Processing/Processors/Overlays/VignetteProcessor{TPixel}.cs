// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
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

        public VignetteProcessor(VignetteProcessor definition)
        {
            this.definition = definition;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(definition.GraphicsOptions);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            TPixel vignetteColor = this.definition.VignetteColor.ToPixel<TPixel>();
            Vector2 centre = Rectangle.Center(sourceRectangle);

            Size sourceSize = source.Size();
            float finalRadiusX = this.definition.RadiusX.Calculate(sourceSize);
            float finalRadiusY = this.definition.RadiusY.Calculate(sourceSize);
            float rX = finalRadiusX > 0
                           ? MathF.Min(finalRadiusX, sourceRectangle.Width * .5F)
                           : sourceRectangle.Width * .5F;
            float rY = finalRadiusY > 0
                           ? MathF.Min(finalRadiusY, sourceRectangle.Height * .5F)
                           : sourceRectangle.Height * .5F;
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
                    configuration,
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