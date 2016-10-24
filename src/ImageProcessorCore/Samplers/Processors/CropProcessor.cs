// <copyright file="CropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class CropProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            int sourceX = sourceRectangle.X;
            int sourceY = sourceRectangle.Y;

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                Parallel.For(
                    startY,
                    endY,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                targetPixels[x, y] = sourcePixels[x + sourceX, y + sourceY];
                            }
                        });
            }
        }
    }
}