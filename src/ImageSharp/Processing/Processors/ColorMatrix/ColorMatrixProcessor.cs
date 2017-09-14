// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The color matrix filter. Inherit from this class to perform operation involving color matrices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class ColorMatrixProcessor<TPixel> : ImageProcessor<TPixel>, IColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public abstract Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public virtual bool Compand { get; set; } = true;

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
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

            Matrix4x4 matrix = this.Matrix;
            bool compand = this.Compand;

            Parallel.For(
                minY,
                maxY,
                configuration.ParallelOptions,
                y =>
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y - startY);

                    for (int x = minX; x < maxX; x++)
                    {
                        ref TPixel pixel = ref row[x - startX];
                        var vector = pixel.ToVector4();

                        if (compand)
                        {
                            vector = vector.Expand();
                        }

                        vector = Vector4.Transform(vector, matrix);
                        pixel.PackFromVector4(compand ? vector.Compress() : vector);
                    }
                });
        }
    }
}