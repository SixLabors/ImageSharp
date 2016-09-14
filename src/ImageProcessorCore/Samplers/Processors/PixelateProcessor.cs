// <copyright file="PixelateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageSampler{TColor,TPacked}"/> to pixelate the colors of an <see cref="Image{TColor, TPacked}"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class PixelateProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelateProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="size">The size of the pixels. Must be greater than 0.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="size"/> is less than 0 or equal to 0.
        /// </exception>
        public PixelateProcessor(int size)
        {
            Guard.MustBeGreaterThan(size, 0, nameof(size));
            this.Value = size;
        }

        /// <summary>
        /// Gets or the pixel size.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int size = this.Value;
            int offset = this.Value / 2;

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

            // Get the range on the y-plane to choose from.
            IEnumerable<int> range = EnumerableExtensions.SteppedRange(minY, i => i < maxY, size);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                Parallel.ForEach(
                    range,
                    this.ParallelOptions,
                    y =>
                        {
                            int offsetY = y - startY;
                            int offsetPy = offset;

                            for (int x = minX; x < maxX; x += size)
                            {
                                int offsetX = x - startX;
                                int offsetPx = offset;

                                // Make sure that the offset is within the boundary of the image.
                                while (offsetY + offsetPy >= maxY)
                                {
                                    offsetPy--;
                                }

                                while (x + offsetPx >= maxX)
                                {
                                    offsetPx--;
                                }

                                // Get the pixel color in the centre of the soon to be pixelated area.
                                // ReSharper disable AccessToDisposedClosure
                                TColor pixel = sourcePixels[offsetX + offsetPx, offsetY + offsetPy];

                                // For each pixel in the pixelate size, set it to the centre color.
                                for (int l = offsetY; l < offsetY + size && l < maxY; l++)
                                {
                                    for (int k = offsetX; k < offsetX + size && k < maxX; k++)
                                    {
                                        targetPixels[k, l] = pixel;
                                    }
                                }
                            }

                            this.OnRowProcessed();
                        });
            }
        }
    }
}