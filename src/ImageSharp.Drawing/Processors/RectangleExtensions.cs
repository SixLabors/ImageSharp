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
        /// Converts a Shaper2D <see cref="SixLabors.Shapes.Rectangle"/> to an ImageSharp <see cref="RectangleF"/>.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>A <see cref="RectangleF"/> representation of this <see cref="SixLabors.Shapes.Rectangle"/></returns>
        public static RectangleF Convert(this SixLabors.Shapes.Rectangle source)
        {
            return new RectangleF(source.Location.X, source.Location.Y, source.Size.Width, source.Size.Height);
        }
    }
}