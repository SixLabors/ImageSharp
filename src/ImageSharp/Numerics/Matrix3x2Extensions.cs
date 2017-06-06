// <copyright file="Matrix3x2Extensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Extension methods for the <see cref="Matrix3x2"/> struct
    /// </summary>
    public static class Matrix3x2Extensions
    {
        /// <summary>
        /// Creates a rotation matrix for the given rotation in degrees and a center point.
        /// </summary>
        /// <param name="degree">The angle in degrees</param>
        /// <param name="centerPoint">The center point</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 CreateRotation(float degree, Point centerPoint)
        {
            float radian = MathF.DegreeToRadian(degree);
            return Matrix3x2.CreateRotation(radian, new Vector2(centerPoint.X, centerPoint.Y));
        }

        /// <summary>
        /// Creates a rotation matrix for the given rotation in degrees and a center point.
        /// </summary>
        /// <param name="degree">The angle in degrees</param>
        /// <param name="centerPoint">The center point</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 CreateRotation(float degree, PointF centerPoint)
        {
            float radian = MathF.DegreeToRadian(degree);
            return Matrix3x2.CreateRotation(radian, centerPoint);
        }

        /// <summary>
        /// Creates a skew matrix for the given angle in degrees and a center point.
        /// </summary>
        /// <param name="degreesX">The x-angle in degrees</param>
        /// <param name="degreesY">The y-angle in degrees</param>
        /// <param name="centerPoint">The center point</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 CreateSkew(float degreesX, float degreesY, Point centerPoint)
        {
            float radiansX = MathF.DegreeToRadian(degreesX);
            float radiansY = MathF.DegreeToRadian(degreesY);
            return Matrix3x2.CreateSkew(radiansX, radiansY, new Vector2(centerPoint.X, centerPoint.Y));
        }

        /// <summary>
        /// Creates a skew matrix for the given angle in degrees and a center point.
        /// </summary>
        /// <param name="degreesX">The x-angle in degrees</param>
        /// <param name="degreesY">The y-angle in degrees</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        /// <param name="centerPoint">The center point</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 CreateSkew(float degreesX, float degreesY, PointF centerPoint)
        {
            float radiansX = MathF.DegreeToRadian(degreesX);
            float radiansY = MathF.DegreeToRadian(degreesY);
            return Matrix3x2.CreateSkew(radiansX, radiansY, new Vector2(centerPoint.X, centerPoint.Y));
        }
    }
}
