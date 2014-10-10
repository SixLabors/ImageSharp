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
    public class ImageMaths
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
        public Rectangle GetBoundingRectangle(Point topLeft, Point bottomRight)
        {
            return new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }

        /// <summary>
        /// Gets a <see cref="Rectangle"/> centered within it's parent.
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
        public Rectangle CenteredRectangle(Rectangle parent, Rectangle child)
        {
            if (parent.Size.Width < child.Size.Width && parent.Size.Height < child.Size.Height)
            {
                return parent;
            }

            int x = (parent.Width - child.Width) / 2;
            int y = (parent.Height - child.Height) / 2;

            return new Rectangle(x, y, child.Width, child.Height);
        }
    }
}
