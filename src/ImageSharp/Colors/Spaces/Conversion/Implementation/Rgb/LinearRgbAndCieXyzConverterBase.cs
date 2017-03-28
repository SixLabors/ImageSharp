// <copyright file="LinearRgbAndCieXyzConverterBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.Rgb
{
    using System.Numerics;

    /// <summary>
    /// Provides base methods for converting between Rgb and CieXyz color spaces.
    /// </summary>
    internal abstract class LinearRgbAndCieXyzConverterBase
    {
        /// <summary>
        /// Geturns the correct matrix to convert between the Rgb and CieXyz color space.
        /// </summary>
        /// <param name="workingSpace">The Rgb working space.</param>
        /// <returns>The <see cref="Matrix4x4"/> based on the chromaticity and working space.</returns>
        public static Matrix4x4 GetRgbToCieXyzMatrix(IRgbWorkingSpace workingSpace)
        {
            DebugGuard.NotNull(workingSpace, nameof(workingSpace));

            RgbPrimariesChromaticityCoordinates chromaticity = workingSpace.ChromaticityCoordinates;

            float xr = chromaticity.R.X;
            float xg = chromaticity.G.X;
            float xb = chromaticity.B.X;
            float yr = chromaticity.R.Y;
            float yg = chromaticity.G.Y;
            float yb = chromaticity.B.Y;

            float mXr = xr / yr;
            const float Yr = 1;
            float mZr = (1 - xr - yr) / yr;

            float mXg = xg / yg;
            const float Yg = 1;
            float mZg = (1 - xg - yg) / yg;

            float mXb = xb / yb;
            const float Yb = 1;
            float mZb = (1 - xb - yb) / yb;

            Matrix4x4 xyzMatrix = new Matrix4x4
            {
                M11 = mXr, M12 = mXg, M13 = mXb,
                M21 = Yr,  M22 = Yg,  M23 = Yb,
                M31 = mZr, M32 = mZg, M33 = mZb,
                M44 = 1F
            };

            Matrix4x4 inverseXyzMatrix;
            Matrix4x4.Invert(xyzMatrix, out inverseXyzMatrix);

            Vector3 vector = Vector3.Transform(workingSpace.WhitePoint.Vector, inverseXyzMatrix);

            // TODO: Is there a built in method for this?
            return new Matrix4x4
            {
                M11 = vector.X * mXr, M12 = vector.Y * mXg, M13 = vector.Z * mXb,
                M21 = vector.X * Yr,  M22 = vector.Y * Yg,  M23 = vector.Z * Yb,
                M31 = vector.X * mZr, M32 = vector.Y * mZg, M33 = vector.Z * mZb,
                M44 = 1F
            };
        }
    }
}