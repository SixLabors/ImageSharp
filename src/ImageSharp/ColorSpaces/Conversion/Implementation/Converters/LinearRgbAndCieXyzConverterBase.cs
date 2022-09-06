// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Provides base methods for converting between <see cref="LinearRgb"/> and <see cref="CieXyz"/> color spaces.
    /// </summary>
    internal abstract class LinearRgbAndCieXyzConverterBase
    {
        /// <summary>
        /// Returns the correct matrix to convert between the Rgb and CieXyz color space.
        /// </summary>
        /// <param name="workingSpace">The Rgb working space.</param>
        /// <returns>The <see cref="Matrix4x4"/> based on the chromaticity and working space.</returns>
        public static Matrix4x4 GetRgbToCieXyzMatrix(RgbWorkingSpace workingSpace)
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
            float mZr = (1 - xr - yr) / yr;

            float mXg = xg / yg;
            float mZg = (1 - xg - yg) / yg;

            float mXb = xb / yb;
            float mZb = (1 - xb - yb) / yb;

            Matrix4x4 xyzMatrix = new()
            {
                M11 = mXr,
                M21 = mXg,
                M31 = mXb,
                M12 = 1F,
                M22 = 1F,
                M32 = 1F,
                M13 = mZr,
                M23 = mZg,
                M33 = mZb,
                M44 = 1F
            };

            Matrix4x4.Invert(xyzMatrix, out Matrix4x4 inverseXyzMatrix);

            Vector3 vector = Vector3.Transform(workingSpace.WhitePoint.ToVector3(), inverseXyzMatrix);

            // Use transposed Rows/Columns
            // TODO: Is there a built in method for this multiplication?
            return new Matrix4x4
            {
                M11 = vector.X * mXr,
                M21 = vector.Y * mXg,
                M31 = vector.Z * mXb,
                M12 = vector.X * 1,
                M22 = vector.Y * 1,
                M32 = vector.Z * 1,
                M13 = vector.X * mZr,
                M23 = vector.Y * mZg,
                M33 = vector.Z * mZb,
                M44 = 1F
            };
        }
    }
}
