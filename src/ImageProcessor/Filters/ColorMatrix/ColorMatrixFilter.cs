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
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            ColorMatrix matrix = this.Value;
            Bgra32 previousColor = source[0, 0];
            Bgra32 pixelValue = this.ApplyMatrix(previousColor, matrix);

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Bgra32 sourceColor = source[x, y];

                                // Check if this is the same as the last pixel. If so use that value
                                // rather than calculating it again. This is an inexpensive optimization.
                                if (sourceColor != previousColor)
                                {
                                    // Perform the operation on the pixel.
                                    pixelValue = this.ApplyMatrix(sourceColor, matrix);

                                    // And setup the previous pointer
                                    previousColor = sourceColor;
                                }

                                target[x, y] = pixelValue;
                            }
                        }
                    });
        }

        /// <summary>
        /// Applies the color matrix against the given color.
        /// </summary>
        /// <param name="sourceColor">The source color.</param>
        /// <param name="matrix">The matrix.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>.
        /// </returns>
        private Bgra32 ApplyMatrix(Bgra32 sourceColor, ColorMatrix matrix)
        {
            bool gamma = this.GammaAdjust;

            if (gamma)
            {
                sourceColor = PixelOperations.ToLinear(sourceColor);
            }

            int sr = sourceColor.R;
            int sg = sourceColor.G;
            int sb = sourceColor.B;
            int sa = sourceColor.A;

            // TODO: Investigate RGBAW
            byte r = ((sr * matrix.Matrix00) + (sg * matrix.Matrix10) + (sb * matrix.Matrix20) + (sa * matrix.Matrix30) + (255f * matrix.Matrix40)).ToByte();
            byte g = ((sr * matrix.Matrix01) + (sg * matrix.Matrix11) + (sb * matrix.Matrix21) + (sa * matrix.Matrix31) + (255f * matrix.Matrix41)).ToByte();
            byte b = ((sr * matrix.Matrix02) + (sg * matrix.Matrix12) + (sb * matrix.Matrix22) + (sa * matrix.Matrix32) + (255f * matrix.Matrix42)).ToByte();
            byte a = ((sr * matrix.Matrix03) + (sg * matrix.Matrix13) + (sb * matrix.Matrix23) + (sa * matrix.Matrix33) + (255f * matrix.Matrix43)).ToByte();

            // TODO: Fix this.
            return gamma ? (Bgra32)PixelOperations.ToSrgb(new Bgra32(b, g, r, a)) : new Bgra32(b, g, r, a);
        }
    }
}
