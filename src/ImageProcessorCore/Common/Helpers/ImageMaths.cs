// <copyright file="ImageMaths.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
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
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        public static int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }

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
        /// Gets the bounding <see cref="Rectangle"/> from the given points.
        /// </summary>
        /// <param name="topLeft">
        /// The <see cref="Point"/> designating the top left position.
        /// </param>
        /// <param name="bottomRight">
        /// The <see cref="Point"/> designating the bottom right position.
        /// </param>
        /// <returns>
        /// The bounding <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetBoundingRectangle(Point topLeft, Point bottomRight)
        {
            return new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
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
        /// Finds the bounding rectangle based on the first instance of any color component other
        /// than the given one.
        /// </summary>
        /// <param name="bitmap">
        /// The <see cref="Image"/> to search within.
        /// </param>
        /// <param name="componentValue">
        /// The color component value to remove.
        /// </param>
        /// <param name="channel">
        /// The <see cref="RgbaComponent"/> channel to test against.
        /// </param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetFilteredBoundingRectangle(ImageBase bitmap, float componentValue, RgbaComponent channel = RgbaComponent.B)
        {
            const float Epsilon = .00001f;
            int width = bitmap.Width;
            int height = bitmap.Height;
            Point topLeft = new Point();
            Point bottomRight = new Point();

            Func<ImageBase, int, int, float, bool> delegateFunc;

            // Determine which channel to check against
            switch (channel)
            {
                case RgbaComponent.R:
                    delegateFunc = (imageBase, x, y, b) => Math.Abs(imageBase[x, y].R - b) > Epsilon;
                    break;

                case RgbaComponent.G:
                    delegateFunc = (imageBase, x, y, b) => Math.Abs(imageBase[x, y].G - b) > Epsilon;
                    break;

                case RgbaComponent.A:
                    delegateFunc = (imageBase, x, y, b) => Math.Abs(imageBase[x, y].A - b) > Epsilon;
                    break;

                default:
                    delegateFunc = (imageBase, x, y, b) => Math.Abs(imageBase[x, y].B - b) > Epsilon;
                    break;
            }

            Func<ImageBase, int> getMinY = imageBase =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(imageBase, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return 0;
            };

            Func<ImageBase, int> getMaxY = imageBase =>
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(imageBase, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return height;
            };

            Func<ImageBase, int> getMinX = imageBase =>
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(imageBase, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return 0;
            };

            Func<ImageBase, int> getMaxX = imageBase =>
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(imageBase, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return height;
            };

            topLeft.Y = getMinY(bitmap);
            topLeft.X = getMinX(bitmap);
            bottomRight.Y = (getMaxY(bitmap) + 1).Clamp(0, height);
            bottomRight.X = (getMaxX(bitmap) + 1).Clamp(0, width);

            return GetBoundingRectangle(topLeft, bottomRight);
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
