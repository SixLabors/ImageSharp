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
                topLeft.Y = getMinY(fastBitmap) + 1;
                topLeft.X = getMinX(fastBitmap) + 1;
                bottomRight.Y = getMaxY(fastBitmap);
                bottomRight.X = getMaxX(fastBitmap);
            }

            return ImageMaths.GetBoundingRectangle(topLeft, bottomRight);
        }

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
    }
}
