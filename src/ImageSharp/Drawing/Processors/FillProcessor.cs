// <copyright file="FillProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using Drawing;
    using ImageSharp.Processors;

    /// <summary>
    /// Using the bursh as a source of pixels colors blends the brush color with source.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class FillProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
        private const float Epsilon = 0.001f;

        private readonly IBrush<TColor, TPacked> brush;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush to source pixel colors from.</param>
        public FillProcessor(IBrush<TColor, TPacked> brush)
        {
            this.brush = brush;
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;

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

            // we could possibly do some optermising by having knowledge about the individual brushes operate
            // for example If brush is SolidBrush<TColor, TPacked> then we could just get the color upfront
            // and skip using the IBrushApplicator<TColor, TPacked>?.
            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (IBrushApplicator<TColor, TPacked> applicator = this.brush.CreateApplicator(sourceRectangle))
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        int offsetY = y - startY;

                        Vector2 currentPoint = default(Vector2);
                        for (int x = minX; x < maxX; x++)
                        {
                            int offsetX = x - startX;
                            int offsetColorX = x - minX;
                            currentPoint.X = offsetX;
                            currentPoint.Y = offsetY;

                            Vector4 backgroundVector = sourcePixels[offsetX, offsetY].ToVector4();
                            Vector4 sourceVector = applicator.GetColor(currentPoint).ToVector4();

                            var finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, 1);

                            TColor packed = default(TColor);
                            packed.PackFromVector4(finalColor);
                            sourcePixels[offsetX, offsetY] = packed;
                        }
                    });
            }
        }
    }
}