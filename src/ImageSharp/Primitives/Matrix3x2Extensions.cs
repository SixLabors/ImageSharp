// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Matrix3x2"/> struct.
    /// </summary>
    public static class Matrix3x2Extensions
    {
        /// <summary>
        /// Creates a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>A translation matrix.</returns>
        public static Matrix3x2 CreateTranslation(PointF position) => Matrix3x2.CreateTranslation(position);

        /// <summary>
        /// Creates a scale matrix that is offset by a given center point.
        /// </summary>
        /// <param name="xScale">Value to scale by on the X-axis.</param>
        /// <param name="yScale">Value to scale by on the Y-axis.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix3x2 CreateScale(float xScale, float yScale, PointF centerPoint) => Matrix3x2.CreateScale(xScale, yScale, centerPoint);

        /// <summary>
        /// Creates a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The scale to use.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix3x2 CreateScale(SizeF scales) => Matrix3x2.CreateScale(scales);

        /// <summary>
        /// Creates a scale matrix from the given vector scale with an offset from the given center point.
        /// </summary>
        /// <param name="scales">The scale to use.</param>
        /// <param name="centerPoint">The center offset.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix3x2 CreateScale(SizeF scales, PointF centerPoint) => Matrix3x2.CreateScale(scales, centerPoint);

        /// <summary>
        /// Creates a scale matrix that scales uniformly with the given scale with an offset from the given center.
        /// </summary>
        /// <param name="scale">The uniform scale to use.</param>
        /// <param name="centerPoint">The center offset.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix3x2 CreateScale(float scale, PointF centerPoint) => Matrix3x2.CreateScale(scale, centerPoint);

        /// <summary>
        /// Creates a skew matrix from the given angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix3x2 CreateSkewDegrees(float degreesX, float degreesY) => Matrix3x2.CreateSkew(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY));

        /// <summary>
        /// Creates a skew matrix from the given angles in radians and a center point.
        /// </summary>
        /// <param name="radiansX">The X angle, in radians.</param>
        /// <param name="radiansY">The Y angle, in radians.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix3x2 CreateSkew(float radiansX, float radiansY, PointF centerPoint) => Matrix3x2.CreateSkew(radiansX, radiansY, centerPoint);

        /// <summary>
        /// Creates a skew matrix from the given angles in degrees and a center point.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix3x2 CreateSkewDegrees(float degreesX, float degreesY, PointF centerPoint) => Matrix3x2.CreateSkew(GeometryUtilities.DegreeToRadian(degreesX), GeometryUtilities.DegreeToRadian(degreesY), centerPoint);

        /// <summary>
        /// Creates a rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix3x2 CreateRotationDegrees(float degrees) => Matrix3x2.CreateRotation(GeometryUtilities.DegreeToRadian(degrees));

        /// <summary>
        /// Creates a rotation matrix using the given rotation in radians and a center point.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix3x2 CreateRotation(float radians, PointF centerPoint) => Matrix3x2.CreateRotation(radians, centerPoint);

        /// <summary>
        /// Creates a rotation matrix using the given rotation in degrees and a center point.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix3x2 CreateRotationDegrees(float degrees, PointF centerPoint) => Matrix3x2.CreateRotation(GeometryUtilities.DegreeToRadian(degrees), centerPoint);
    }
}
