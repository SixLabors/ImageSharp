// <copyright file="HueProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor, TPacked}"/> to change the hue of an <see cref="Image{TColor, TPacked}"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class HueProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HueProcessor{TColor, TPacked}"/> class.
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

            float radians = ImageMaths.DegreesToRadians(angle);
            double cosradians = Math.Cos(radians);
            double sinradians = Math.Sin(radians);

            float lumR = .213f;
            float lumG = .715f;
            float lumB = .072f;

            float oneMinusLumR = 1 - lumR;
            float oneMinusLumG = 1 - lumG;
            float oneMinusLumB = 1 - lumB;

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            Matrix4x4 matrix4X4 = new Matrix4x4()
            {
                M11 = (float)(lumR + (cosradians * oneMinusLumR) - (sinradians * lumR)),
                M12 = (float)(lumR - (cosradians * lumR) - (sinradians * 0.143)),
                M13 = (float)(lumR - (cosradians * lumR) - (sinradians * oneMinusLumR)),
                M21 = (float)(lumG - (cosradians * lumG) - (sinradians * lumG)),
                M22 = (float)(lumG + (cosradians * oneMinusLumG) + (sinradians * 0.140)),
                M23 = (float)(lumG - (cosradians * lumG) + (sinradians * lumG)),
                M31 = (float)(lumB - (cosradians * lumB) + (sinradians * oneMinusLumB)),
                M32 = (float)(lumB - (cosradians * lumB) - (sinradians * 0.283)),
                M33 = (float)(lumB + (cosradians * oneMinusLumB) + (sinradians * lumB))
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
