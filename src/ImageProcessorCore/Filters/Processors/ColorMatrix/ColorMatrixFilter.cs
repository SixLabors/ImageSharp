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
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public abstract class ColorMatrixFilter<T, TP> : ImageProcessor<T, TP>, IColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public abstract Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public override bool Compand { get; set; } = true;

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
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
                                targetPixels[offsetX, offsetY] = this.ApplyMatrix(sourcePixels[offsetX, offsetY], matrix, compand);
                            }

                            this.OnRowProcessed();
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
        private T ApplyMatrix(T color, Matrix4x4 matrix, bool compand)
        {
            Vector4 vector = color.ToVector4();

            if (compand)
            {
                vector = vector.Expand();
            }

            Vector3 transformed = Vector3.Transform(new Vector3(vector.X, vector.Y, vector.Z), matrix);
            vector = new Vector4(transformed, vector.W);
            T packed = default(T);
            packed.PackFromVector4(compand ? vector.Compress() : vector);
            return packed;
        }
    }
}
