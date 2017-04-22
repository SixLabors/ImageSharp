// <copyright file="HueProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// An <see cref="ImageProcessor{TPixel}"/> to change the hue of an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class HueProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HueProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="angle">The new brightness of the image. Must be between -100 and 100.</param>
        public HueProcessor(float angle)
        {
            // Wrap the angle round at 360.
            angle = angle % 360;

            // Make sure it's not negative.
            while (angle < 0)
            {
                angle += 360;
            }

            this.Angle = angle;

            float radians = MathF.DegreeToRadian(angle);
            float cosradians = MathF.Cos(radians);
            float sinradians = MathF.Sin(radians);

            float lumR = .213F;
            float lumG = .715F;
            float lumB = .072F;

            float oneMinusLumR = 1 - lumR;
            float oneMinusLumG = 1 - lumG;
            float oneMinusLumB = 1 - lumB;

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            Matrix4x4 matrix4X4 = new Matrix4x4()
            {
                M11 = lumR + (cosradians * oneMinusLumR) - (sinradians * lumR),
                M12 = lumR - (cosradians * lumR) - (sinradians * 0.143F),
                M13 = lumR - (cosradians * lumR) - (sinradians * oneMinusLumR),
                M21 = lumG - (cosradians * lumG) - (sinradians * lumG),
                M22 = lumG + (cosradians * oneMinusLumG) + (sinradians * 0.140F),
                M23 = lumG - (cosradians * lumG) + (sinradians * lumG),
                M31 = lumB - (cosradians * lumB) + (sinradians * oneMinusLumB),
                M32 = lumB - (cosradians * lumB) - (sinradians * 0.283F),
                M33 = lumB + (cosradians * oneMinusLumB) + (sinradians * lumB),
                M44 = 1
            };

            this.Matrix = matrix4X4;
        }

        /// <summary>
        /// Gets the rotation value.
        /// </summary>
        public float Angle { get; }

        /// <inheritdoc/>
        public override Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
