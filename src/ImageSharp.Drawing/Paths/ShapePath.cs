// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Drawing
{
    /// <summary>
    /// A mapping between a <see cref="IPath"/> and a region.
    /// </summary>
    internal class ShapePath : ShapeRegion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapePath"/> class.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="pen">The pen to apply to the shape.</param>
        // SixLabors.shape willbe moving to a Span/ReadOnlySpan based API shortly use ToArray for now.
        public ShapePath(IPath shape, Pens.IPen pen)
            : base(shape.GenerateOutline(pen.StrokeWidth, pen.StrokePattern.ToArray()))
        {
        }
    }
}
