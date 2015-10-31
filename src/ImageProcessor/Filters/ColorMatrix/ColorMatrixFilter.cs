// <copyright file="ColorMatrixFilter.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Threading.Tasks;

    /// <summary>
    /// The color matrix filter.
    /// </summary>
    public class ColorMatrixFilter : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorMatrixFilter"/> class.
        /// </summary>
        /// <param name="matrix">The matrix to apply.</param>
        /// <param name="gammaAdjust">Whether to gamma adjust the colors before applying the matrix.</param>
        public ColorMatrixFilter(ColorMatrix matrix, bool gammaAdjust)
        {
            this.Value = matrix;
            this.GammaAdjust = gammaAdjust;
        }

        /// <summary>
        /// Gets the matrix value.
        /// </summary>
        public ColorMatrix Value { get; }

        /// <summary>
        /// Gets a value indicating whether to gamma adjust the colors before applying the matrix.
        /// </summary>
        public bool GammaAdjust { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            bool gamma = this.GammaAdjust;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            ColorMatrix matrix = this.Value;

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                target[x, y] = ApplyMatrix(source[x, y], matrix, gamma);
                            }
                        }
                    });
        }

        /// <summary>
        /// Applies the color matrix against the given color.
        /// </summary>
        /// <param name="color">The source color.</param>
        /// <param name="matrix">The matrix.</param>
        /// <param name="gamma">Whether to perform gamma adjustments.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        private static Color ApplyMatrix(Color color, ColorMatrix matrix, bool gamma)
        {
            if (gamma)
            {
                color = PixelOperations.ToLinear(color);
            }

            float sr = color.R;
            float sg = color.G;
            float sb = color.B;
            float sa = color.A;

            // TODO: Investigate RGBAW
            color.R = (sr * matrix.Matrix00) + (sg * matrix.Matrix10) + (sb * matrix.Matrix20) + (sa * matrix.Matrix30) + matrix.Matrix40;
            color.G = (sr * matrix.Matrix01) + (sg * matrix.Matrix11) + (sb * matrix.Matrix21) + (sa * matrix.Matrix31) + matrix.Matrix41;
            color.B = (sr * matrix.Matrix02) + (sg * matrix.Matrix12) + (sb * matrix.Matrix22) + (sa * matrix.Matrix32) + matrix.Matrix42;
            color.A = (sr * matrix.Matrix03) + (sg * matrix.Matrix13) + (sb * matrix.Matrix23) + (sa * matrix.Matrix33) + matrix.Matrix43;

            return gamma ? PixelOperations.ToSrgb(color) : color;
        }
    }
}
