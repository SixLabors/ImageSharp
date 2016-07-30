// <copyright file="BackgroundColorProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    public class BackgroundColorProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="color">The <see cref="T"/> to set the background color to.</param>
        public BackgroundColorProcessor(T color)
        {
            this.Value = color;
        }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public T Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector4 backgroundColor = this.Value.ToVector4();

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    startY,
                    endY,
                    this.ParallelOptions,
                    y =>
                        {
                            if (y >= sourceY && y < sourceBottom)
                            {
                                for (int x = startX; x < endX; x++)
                                {
                                    Vector4 color = sourcePixels[x, y].ToVector4();
                                    float a = color.W;

                                    if (a < 1 && a > 0)
                                    {
                                        color = Vector4.Lerp(color, backgroundColor, .5f);
                                    }

                                    if (Math.Abs(a) < Epsilon)
                                    {
                                        color = backgroundColor;
                                    }

                                    T packed = default(T);
                                    packed.PackVector(color);
                                    targetPixels[x, y] = packed;
                                }

                                this.OnRowProcessed();
                            }
                        });
            }
        }
    }
}
