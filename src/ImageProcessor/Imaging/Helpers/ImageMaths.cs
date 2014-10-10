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
    using System.Drawing;

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
