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
    public class BackgroundColorProcessor<TColor, TPacked> : ImageFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="color">The <see cref="TColor"/> to set the background color to.</param>
        public BackgroundColorProcessor(TColor color)
        {
            this.Value = color;
        }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public TColor Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle, int startY, int endY)
        {
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

            Vector4 backgroundColor = this.Value.ToVector4();

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
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
                            Vector4 color = sourcePixels[offsetX, offsetY].ToVector4();
                            float a = color.W;

                            if (a < 1 && a > 0)
                            {
                                color = Vector4.Lerp(color, backgroundColor, .5F);
                            }

                            if (Math.Abs(a) < Epsilon)
                            {
                                color = backgroundColor;
                            }

                            TColor packed = default(TColor);
                            packed.PackFromVector4(color);
                            sourcePixels[offsetX, offsetY] = packed;
                        }

                        this.OnRowProcessed();
                    });
            }
        }
    }
}