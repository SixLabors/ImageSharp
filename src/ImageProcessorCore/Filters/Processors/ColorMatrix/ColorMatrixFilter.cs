// <copyright file="ColorMatrixFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// The color matrix filter.
    /// </summary>
    public abstract class ColorMatrixFilter<T, TP> : ImageProcessor<T, TP>, IColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public abstract Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public virtual bool Compand => true;

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Matrix4x4 matrix = this.Matrix;

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            targetPixels[x, y] = this.ApplyMatrix(sourcePixels[x, y], matrix);
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
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        private T ApplyMatrix(T color, Matrix4x4 matrix)
        {
            bool compand = this.Compand;

            //if (compand)
            //{
            //    color = Color.Expand(color);
            //}

            Vector4 transformed = Vector4.Transform(color.ToVector4(), matrix);
            //Vector3 transformed = Vector3.Transform(color.ToVector3(), matrix);
            //return compand ? Color.Compress(new Color(transformed, color.A)) : new Color(transformed, color.A);
            T packed = default(T);
            packed.PackVector(transformed);
            return packed;
        }
    }
}
