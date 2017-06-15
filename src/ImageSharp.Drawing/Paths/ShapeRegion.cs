// <copyright file="ShapeRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System;
    using System.Buffers;
    using System.Numerics;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    /// <summary>
    /// A mapping between a <see cref="IPath"/> and a region.
    /// </summary>
    internal class ShapeRegion : Region
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapeRegion"/> class.
        /// </summary>
        /// <param name="shape">The shape.</param>
        public ShapeRegion(IPath shape)
        {
            this.Shape = shape.AsClosedPath();
            int left = (int)MathF.Floor(shape.Bounds.Left);
            int top = (int)MathF.Floor(shape.Bounds.Top);

            int right = (int)MathF.Ceiling(shape.Bounds.Right);
            int bottom = (int)MathF.Ceiling(shape.Bounds.Bottom);
            this.Bounds = Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Gets the fillable shape
        /// </summary>
        public IPath Shape { get; }

        /// <inheritdoc/>
        public override int MaxIntersections => this.Shape.MaxIntersections;

        /// <inheritdoc/>
        public override Rectangle Bounds { get; }

        /// <inheritdoc/>
        public override int Scan(float y, Span<float> buffer)
        {
            PointF start = new PointF(this.Bounds.Left - 1, y);
            PointF end = new PointF(this.Bounds.Right + 1, y);
            PointF[] innerbuffer = ArrayPool<PointF>.Shared.Rent(buffer.Length);
            try
            {
                int count = this.Shape.FindIntersections(start, end, innerbuffer);

                for (int i = 0; i < count; i++)
                {
                    buffer[i] = innerbuffer[i].X;
                }

                return count;
            }
            finally
            {
                ArrayPool<PointF>.Shared.Return(innerbuffer);
            }
        }
    }
}
