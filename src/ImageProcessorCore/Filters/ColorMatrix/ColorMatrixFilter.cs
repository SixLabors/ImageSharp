// <copyright file="ColorMatrixFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// The color matrix filter.
    /// </summary>
    public abstract class ColorMatrixFilter : ParallelImageProcessor, IColorMatrixFilter
    {
        /// <inheritdoc/>
        public abstract Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public virtual bool Compand => true;

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Matrix4x4 matrix = this.Matrix;

            using (PixelAccessor sourcePixels = source.Lock())
            using (PixelAccessor targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                targetPixels[x, y] = this.ApplyMatrix(sourcePixels[x, y], matrix);
                            }

                            this.OnRowProcessed();
                        }
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
        private Color ApplyMatrix(Color color, Matrix4x4 matrix)
        {
            bool compand = this.Compand;

            if (compand)
            {
                color = Color.Expand(color);
            }

            Vector3 transformed = Vector3.Transform(color.ToVector3(), matrix);
            return compand ? Color.Compress(new Color(transformed, color.A)) : new Color(transformed, color.A);
        }
    }
}
