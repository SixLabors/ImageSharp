// <copyright file="RectangleExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;

    /// <summary>
    /// Extension methods for helping to bridge Shaper2D and ImageSharp primitives.
    /// </summary>
    internal static class RectangleExtensions
    {
        /// <summary>
        /// Converts a Shaper2D <see cref="SixLabors.Shapes.Rectangle"/> to an ImageSharp <see cref="Rectangle"/> by creating a <see cref="Rectangle"/> the entirely surrounds the source.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>A <see cref="Rectangle"/> representation of this <see cref="SixLabors.Shapes.Rectangle"/></returns>
        public static Rectangle Convert(this SixLabors.Shapes.Rectangle source)
        {
            int left = (int)Math.Floor(source.Left);
            int right = (int)Math.Ceiling(source.Right);
            int top = (int)Math.Floor(source.Top);
            int bottom = (int)Math.Ceiling(source.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }
    }
}