// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageMaths.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides reusable mathematical methods to apply to images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Helpers
{
    using System;
    using System.Drawing;

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Provides reusable mathematical methods to apply to images.
    /// </summary>
    public static class ImageMaths
    {
        /// <summary>
        /// Gets a <see cref="Rectangle"/> representing the child centered relative to the parent.
        /// </summary>
        /// <param name="parent">
        /// The parent <see cref="Rectangle"/>.
        /// </param>
        /// <param name="child">
        /// The child <see cref="Rectangle"/>.
        /// </param>
        /// <returns>
        /// The centered <see cref="Rectangle"/>.
        /// </returns>
        public static RectangleF CenteredRectangle(Rectangle parent, Rectangle child)
        {
            float x = (parent.Width - child.Width) / 2.0F;
            float y = (parent.Height - child.Height) / 2.0F;
            int width = child.Width;
            int height = child.Height;
            return new RectangleF(x, y, width, height);
        }

        /// <summary>
        /// Restricts a value to be within a specified range.
        /// </summary>
        /// <param name="value">
        /// The The value to clamp.
        /// </param>
        /// <param name="min">
        /// The minimum value. If value is less than min, min will be returned.
        /// </param>
        /// <param name="max">
        /// The maximum value. If value is greater than max, max will be returned.
        /// </param>
        /// <typeparam name="T">
        /// The <see cref="System.Type"/> to clamp.
        /// </typeparam>
        /// <returns>
        /// The <see cref="IComparable{T}"/> representing the clamped value.
        /// </returns>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
            {
                return min;
            }

            if (value.CompareTo(max) > 0)
            {
                return max;
            }

            return value;
        }

        /// <summary>
        /// Returns value indicating whether the given number is with in the minimum and maximum
        /// given range.
        /// </summary>
        /// <param name="value">
        /// The The value to clamp.
        /// </param>
        /// <param name="min">
        /// If <paramref name="include"/> 
        /// The minimum range value.
        /// </param>
        /// <param name="max">
        /// The maximum range value.
        /// </param>
        /// <param name="include">
        /// Whether to include the minimum and maximum values.
        /// Defaults to true.
        /// </param>
        /// <typeparam name="T">
        /// The <see cref="System.Type"/> to test.
        /// </typeparam>
        /// <returns>
        /// True if the value falls within the maximum and minimum; otherwise, false.
        /// </returns>
        public static bool InRange<T>(T value, T min, T max, bool include = true) where T : IComparable<T>
        {
            if (include)
            {
                return (value.CompareTo(min) >= 0) && (value.CompareTo(max) <= 0);
            }

            return (value.CompareTo(min) > 0) && (value.CompareTo(max) < 0);
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
            return angleInDegrees * (Math.PI / 180);
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
        public static Rectangle GetFilteredBoundingRectangle(Image bitmap, byte componentValue, RgbaComponent channel = RgbaComponent.B)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            Point topLeft = new Point();
            Point bottomRight = new Point();

            Func<FastBitmap, int, int, byte, bool> delegateFunc;

            // Determine which channel to check against
            switch (channel)
            {
                case RgbaComponent.R:
                    delegateFunc = (fastBitmap, x, y, b) => fastBitmap.GetPixel(x, y).R != b;
                    break;

                case RgbaComponent.G:
                    delegateFunc = (fastBitmap, x, y, b) => fastBitmap.GetPixel(x, y).G != b;
                    break;

                case RgbaComponent.A:
                    delegateFunc = (fastBitmap, x, y, b) => fastBitmap.GetPixel(x, y).A != b;
                    break;

                default:
                    delegateFunc = (fastBitmap, x, y, b) => fastBitmap.GetPixel(x, y).B != b;
                    break;
            }

            Func<FastBitmap, int> getMinY = fastBitmap =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(fastBitmap, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return 0;
            };

            Func<FastBitmap, int> getMaxY = fastBitmap =>
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(fastBitmap, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return height;
            };

            Func<FastBitmap, int> getMinX = fastBitmap =>
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(fastBitmap, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return 0;
            };

            Func<FastBitmap, int> getMaxX = fastBitmap =>
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(fastBitmap, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return height;
            };

            using (FastBitmap fastBitmap = new FastBitmap(bitmap))
            {
                topLeft.Y = getMinY(fastBitmap);
                topLeft.X = getMinX(fastBitmap);
                bottomRight.Y = getMaxY(fastBitmap) + 1;
                bottomRight.X = getMaxX(fastBitmap) + 1;
            }

            return GetBoundingRectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// Rotates one point around another
        /// <see href="http://stackoverflow.com/questions/13695317/rotate-a-point-around-another-point"/>
        /// </summary>
        /// <param name="pointToRotate">The point to rotate.</param>
        /// <param name="angleInDegrees">The rotation angle in degrees.</param>
        /// <param name="centerPoint">The centre point of rotation. If not set the point will equal
        /// <see cref="Point.Empty"/>
        /// </param>
        /// <returns>Rotated point</returns>
        public static Point RotatePoint(Point pointToRotate, double angleInDegrees, Point? centerPoint = null)
        {
            Point center = centerPoint ?? Point.Empty;

            double angleInRadians = DegreesToRadians(angleInDegrees);
            double cosTheta = Math.Cos(angleInRadians);
            double sinTheta = Math.Sin(angleInRadians);
            return new Point
            {
                X =
                    (int)((cosTheta * (pointToRotate.X - center.X)) -
                          ((sinTheta * (pointToRotate.Y - center.Y)) + center.X)),
                Y =
                    (int)((sinTheta * (pointToRotate.X - center.X)) +
                          ((cosTheta * (pointToRotate.Y - center.Y)) + center.Y))
            };
        }

        /// <summary>
        /// Returns the array of <see cref="Point"/> matching the bounds of the given rectangle.
        /// </summary>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> to return the points from.
        /// </param>
        /// <returns>
        /// The <see cref="Point"/> array.
        /// </returns>
        public static Point[] ToPoints(Rectangle rectangle)
        {
            return new[]
            {
                new Point(rectangle.Left, rectangle.Top),
                new Point(rectangle.Right, rectangle.Top),
                new Point(rectangle.Right, rectangle.Bottom),
                new Point(rectangle.Left, rectangle.Bottom)
            };
        }

        /// <summary>
        /// Calculates the zoom needed after the rotation to ensure the canvas is covered
        /// by the rotated image.
        /// </summary>
        /// <param name="imageWidth">Width of the image.</param>
        /// <param name="imageHeight">Height of the image.</param>
        /// <param name="angleInDegrees">The angle in degrees.</param>
        /// <remarks>
        /// Based on <see href="http://math.stackexchange.com/questions/1070853/"/>
        /// </remarks>
        /// <returns>The zoom needed</returns>
        public static float ZoomAfterRotation(int imageWidth, int imageHeight, float angleInDegrees)
        {
            Rectangle rectangle = GetBoundingRotatedRectangle(imageWidth, imageHeight, angleInDegrees);
            return Math.Max((float)rectangle.Width / imageWidth, (float)rectangle.Height / imageHeight);
        }
    }
}