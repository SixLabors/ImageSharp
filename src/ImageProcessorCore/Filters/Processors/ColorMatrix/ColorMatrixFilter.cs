// <copyright file="ColorMatrixFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// The color matrix filter. Inherit from this class to perform operation involving color matrices.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class ColorMatrixFilter<TColor, TPacked> : ImageFilter<TColor, TPacked>, IColorMatrixFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public abstract Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public override bool Compand { get; set; } = true;

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

            Matrix4x4 matrix = this.Matrix;
            bool compand = this.Compand;

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
                                sourcePixels[offsetX, offsetY] = this.ApplyMatrix(sourcePixels[offsetX, offsetY], matrix, compand);
                            }
                        });
            }
        }

        /// <summary>
        /// Applies the color matrix against the given color.
        /// </summary>
        /// <param name="color">The source color.</param>
        /// <param name="matrix">The matrix.</param>
        /// <param name="compand">Whether to compand the color during processing.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        private TColor ApplyMatrix(TColor color, Matrix4x4 matrix, bool compand)
        {
            Vector4 vector = color.ToVector4();

            if (compand)
            {
                vector = vector.Expand();
            }

            Vector3 transformed = Vector3.Transform(new Vector3(vector.X, vector.Y, vector.Z), matrix);
            vector = new Vector4(transformed, vector.W);
            TColor packed = default(TColor);
            packed.PackFromVector4(compand ? vector.Compress() : vector);
            return packed;
        }
    }
}