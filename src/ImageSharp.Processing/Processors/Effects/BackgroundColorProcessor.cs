// <copyright file="BackgroundColorProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class BackgroundColorProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="color">The <typeparamref name="TColor"/> to set the background color to.</param>
        public BackgroundColorProcessor(TColor color)
        {
            this.Value = color;
        }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public TColor Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
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

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
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
                                color = Vector4BlendTransforms.PremultipliedLerp(backgroundColor, color, .5F);
                            }

                            if (Math.Abs(a) < Constants.Epsilon)
                            {
                                color = backgroundColor;
                            }

                            TColor packed = default(TColor);
                            packed.PackFromVector4(color);
                            sourcePixels[offsetX, offsetY] = packed;
                        }
                    });
            }
        }
    }
}