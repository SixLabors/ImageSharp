// <copyright file="RectangleExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Buffers;
    using System.Numerics;
    using System.Threading.Tasks;
    using Drawing;
    using ImageSharp.Processing;
    using SixLabors.Shapes;
    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// Extension methods for helping to bridge Shaper2D and ImageSharp primitives.
    /// </summary>
    internal static class RectangleExtensions
    {
        /// <summary>
        /// Converts a Shaper2D <see cref="SixLabors.Shapes.Rectangle"/> to an ImageSharp <see cref="Rectangle"/> by creating a <see cref="Rectangle"/> the entirely surrounds the source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>A <see cref="Rectangle"/> representation of this <see cref="SixLabors.Shapes.Rectangle"/></returns>
        public static Rectangle Convert(this SixLabors.Shapes.Rectangle source)
        {
            int y = (int)Math.Floor(source.Y);
            int width = (int)Math.Ceiling(source.Size.Width);
            int x = (int)Math.Floor(source.X);
            int height = (int)Math.Ceiling(source.Size.Height);
            return new Rectangle(x, y, width, height);
        }
    }
}