// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// An <see cref="ImageProcessor{TPixel}"/> to change the saturation of an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class SaturationProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SaturationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="saturation">The new saturation of the image. Must be between -100 and 100.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="saturation"/> is less than -100 or is greater than 100.
        /// </exception>
        public SaturationProcessor(int saturation)
        {
            this.Amount = saturation;
            Guard.MustBeBetweenOrEqualTo(saturation, -100, 100, nameof(saturation));
            float saturationFactor = saturation / 100F;

            // Stop at -1 to prevent inversion.
            saturationFactor++;

            // The matrix is set up to "shear" the color space using the following set of values.
            // Note that each color component has an effective luminance which contributes to the
            // overall brightness of the pixel.
            // See http://graficaobscura.com/matrix/index.html
            float saturationComplement = 1.0f - saturationFactor;
            float saturationComplementR = 0.3086f * saturationComplement;
            float saturationComplementG = 0.6094f * saturationComplement;
            float saturationComplementB = 0.0820f * saturationComplement;

            var matrix4X4 = new Matrix4x4
            {
                M11 = saturationComplementR + saturationFactor,
                M12 = saturationComplementR,
                M13 = saturationComplementR,
                M21 = saturationComplementG,
                M22 = saturationComplementG + saturationFactor,
                M23 = saturationComplementG,
                M31 = saturationComplementB,
                M32 = saturationComplementB,
                M33 = saturationComplementB + saturationFactor,
                M44 = 1
            };

            this.Matrix = matrix4X4;
        }

        /// <summary>
        /// Gets the amount to apply.
        /// </summary>
        public int Amount { get; }

        /// <inheritdoc/>
        public override Matrix4x4 Matrix { get; }
    }
}
