// <copyright file="AlphaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{T,TP}"/> to change the alpha component of an <see cref="Image{T,TP}"/>.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class AlphaProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="percent">The percentage to adjust the opacity of the image. Must be between 0 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="percent"/> is less than 0 or is greater than 100.
        /// </exception>
        public AlphaProcessor(int percent)
        {
            Guard.MustBeBetweenOrEqualTo(percent, 0, 100, nameof(percent));
            this.Value = percent;
        }

        /// <summary>
        /// Gets the alpha value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float alpha = this.Value / 100F;
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

            Vector4 alphaVector = new Vector4(1, 1, 1, alpha);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            int offsetY = y - startY;
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                T packed = default(T);
                                packed.PackFromVector4(sourcePixels[offsetX, offsetY].ToVector4() * alphaVector);
                                targetPixels[offsetX, offsetY] = packed;
                            }

                            this.OnRowProcessed();
                        });
            }
        }
    }
}
