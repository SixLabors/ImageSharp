// <copyright file="PixelateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{T,TP}"/> to invert the colors of an <see cref="Image{T,TP}"/>.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class PixelateProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
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
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int size = this.Value;
            int offset = this.Value / 2;

            // Get the range on the y-plane to choose from.
            IEnumerable<int> range = EnumerableExtensions.SteppedRange(startY, i => i < endY, size);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.ForEach(
                    range,
                    y =>
                        {
                            if (y >= sourceY && y < sourceBottom)
                            {
                                for (int x = startX; x < endX; x += size)
                                {
                                    int offsetX = offset;
                                    int offsetY = offset;

                                    // Make sure that the offset is within the boundary of the 
                                    // image.
                                    while (y + offsetY >= sourceBottom)
                                    {
                                        offsetY--;
                                    }

                                    while (x + offsetX >= endX)
                                    {
                                        offsetX--;
                                    }

                                    // Get the pixel color in the centre of the soon to be pixelated area.
                                    // ReSharper disable AccessToDisposedClosure
                                    T pixel = sourcePixels[x + offsetX, y + offsetY];

                                    // For each pixel in the pixelate size, set it to the centre color.
                                    for (int l = y; l < y + size && l < sourceBottom; l++)
                                    {
                                        for (int k = x; k < x + size && k < endX; k++)
                                        {
                                            targetPixels[k, l] = pixel;
                                        }
                                    }
                                }

                                this.OnRowProcessed();
                            }
                        });
            }
        }
    }
}
