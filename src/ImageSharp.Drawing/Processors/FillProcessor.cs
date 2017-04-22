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
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    /// <summary>
    /// Using the bursh as a source of pixels colors blends the brush color with source.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class FillProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The brush.
        /// </summary>
        private readonly IBrush<TPixel> brush;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="brush">The brush to source pixel colors from.</param>
        public FillProcessor(IBrush<TPixel> brush)
        {
            this.brush = brush;
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
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
            // for example If brush is SolidBrush<TPixel> then we could just get the color upfront
            // and skip using the IBrushApplicator<TPixel>?.
            using (PixelAccessor<TPixel> sourcePixels = source.Lock())
            using (BrushApplicator<TPixel> applicator = this.brush.CreateApplicator(sourcePixels, sourceRectangle))
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

                            Vector4 backgroundVector = sourcePixels[offsetX, offsetY].ToVector4();
                            Vector4 sourceVector = applicator[offsetX, offsetY].ToVector4();

                            Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, 1);

                            TPixel packed = default(TPixel);
                            packed.PackFromVector4(finalColor);
                            sourcePixels[offsetX, offsetY] = packed;
                        }
                    });
            }
        }
    }
}