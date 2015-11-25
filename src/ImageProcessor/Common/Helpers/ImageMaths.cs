// <copyright file="ImageMaths.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Provides common mathematical methods.
    /// </summary>
    internal static class ImageMaths
    {
        /// <summary>
        /// Represents PI, the ratio of a circle's circumference to its diameter.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public const float PI = 3.1415926535897931f;

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x).</param>
        /// <param name="sigma">The spread of the blur.</param>
        /// <returns>The Gaussian G(x)</returns>
        public static float Gaussian(float x, float sigma)
        {
            const float Numerator = 1.0f;
            float denominator = (float)(Math.Sqrt(2 * PI) * sigma);

            float exponentNumerator = -x * x;
            float exponentDenominator = (float)(2 * Math.Pow(sigma, 2));

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }

        /// <summary>
        /// Returns the result of a B-C filter against the given value.
        /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <param name="b">The B-Spline curve variable.</param>
        /// <param name="c">The Cardinal curve variable.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        public static float GetBcValue(float x, float b, float c)
        {
            float temp;

            if (x < 0)
            {
                x = -x;
            }

            temp = x * x;
            if (x < 1)
            {
                x = ((12 - (9 * b) - (6 * c)) * (x * temp)) + ((-18 + (12 * b) + (6 * c)) * temp) + (6 - (2 * b));
                return x / 6;
            }

            if (x < 2)
            {
                x = ((-b - (6 * c)) * (x * temp)) + (((6 * b) + (30 * c)) * temp) + (((-12 * b) - (48 * c)) * x) + ((8 * b) + (24 * c));
                return x / 6;
            }

            return 0;
        }

        /// <summary>
        /// Gets the result of a sine cardinal function for the given value.
        /// </summary>
        /// <param name="x">
        /// The value to calculate the result for.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        public static float SinC(float x)
        {
            const float Epsilon = .00001f;

            if (Math.Abs(x) > Epsilon)
            {
                x *= PI;
                return Clean((float)Math.Sin(x) / x);
            }

            return 1.0f;
        }

        /// <summary>
        /// Returns the given degrees converted to radians.
        /// </summary>
        /// <param name="angleInDegrees">
        /// The angle in degrees.
        /// </param>
        /// <returns>
        /// The <see cref="double"/> representing the degree as radians.
        /// </returns>
        public static double DegreesToRadians(double angleInDegrees)
        {
            return angleInDegrees * (PI / 180);
        }

        /// <summary>
        /// Rotates one point around another
        /// <see href="http://stackoverflow.com/a/13695630/82333"/>
        /// </summary>
        /// <param name="point">The point to rotate.</param>
        /// <param name="origin">The origin point of rotation.</param>
        /// <param name="degrees">The rotation angle in degrees.</param>
        /// <returns><see cref="Vector2"/></returns>
        public static Vector2 RotatePoint(Vector2 point, Vector2 origin, float degrees)
        {
            double radians = DegreesToRadians(degrees);
            double cosTheta = Math.Cos(radians);
            double sinTheta = Math.Sin(radians);

            Vector2 translatedPoint = new Vector2
            {
                X = (float)(origin.X
                    + (point.X - origin.X) * cosTheta
                    - (point.Y - origin.Y) * sinTheta),
                Y = (float)(origin.Y
                    + (point.Y - origin.Y) * cosTheta
                    + (point.X - origin.X) * sinTheta)
            };

            return translatedPoint;
        }

        /// <summary>
        /// Calculates the new size after rotation.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="angleInDegrees">The angle of rotation.</param>
        /// <returns>The new size of the image</returns>
        public static Rectangle GetBoundingRotatedRectangle(int width, int height, float angleInDegrees)
        {
            // Check first clockwise.
            double radians = DegreesToRadians(angleInDegrees);
            double radiansSin = Math.Sin(radians);
            double radiansCos = Math.Cos(radians);
            double width1 = (height * radiansSin) + (width * radiansCos);
            double height1 = (width * radiansSin) + (height * radiansCos);

            // Find dimensions in the other direction
            radiansSin = Math.Sin(-radians);
            radiansCos = Math.Cos(-radians);
            double width2 = (height * radiansSin) + (width * radiansCos);
            double height2 = (width * radiansSin) + (height * radiansCos);

            // Get the external vertex for the rotation
            Rectangle result = new Rectangle(
                0,
                0,
                Convert.ToInt32(Math.Max(Math.Abs(width1), Math.Abs(width2))),
                Convert.ToInt32(Math.Max(Math.Abs(height1), Math.Abs(height2))));

            return result;
        }

        /// <summary>
        /// Ensures that any passed double is correctly rounded to zero
        /// </summary>
        /// <param name="x">The value to clean.</param>
        /// <returns>
        /// The <see cref="float"/>
        /// </returns>.
        private static float Clean(float x)
        {
            const float Epsilon = .00001f;

            if (Math.Abs(x) < Epsilon)
            {
                return 0f;
            }

            return x;
        }
    }
}
