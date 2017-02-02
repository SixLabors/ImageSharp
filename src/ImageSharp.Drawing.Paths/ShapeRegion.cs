// <copyright file="ShapeRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System.Buffers;
    using System.Collections.Immutable;
    using System.Numerics;

    using ImageSharp.Drawing.Processors;

    using SixLabors.Shapes;

    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// A drawable mapping between a <see cref="SixLabors.Shapes.IShape"/>/<see cref="SixLabors.Shapes.IPath"/> and a drawable/fillable region.
    /// </summary>
    internal class ShapeRegion : Region
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapeRegion"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public ShapeRegion(IPath path)
            : this(path.AsShape())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShapeRegion"/> class.
        /// </summary>
        /// <param name="shape">The shape.</param>
        public ShapeRegion(IShape shape)
        {
            this.Shape = shape;
            this.Bounds = shape.Bounds.Convert();
        }

        /// <summary>
        /// Gets the fillable shape
        /// </summary>
        public IShape Shape { get; }

        /// <summary>
        /// Gets the maximum number of intersections to could be returned.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        public override int MaxIntersections => this.Shape.MaxIntersections;

        /// <summary>
        /// Gets the bounds.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public override Rectangle Bounds { get; }

        /// <summary>
        /// Scans the X axis for intersections.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>
        /// The number of intersections found.
        /// </returns>
        public override int ScanX(int x, float[] buffer, int length, int offset)
        {
            var start = new Vector2(x, this.Bounds.Top - 1);
            var end = new Vector2(x, this.Bounds.Bottom + 1);
            Vector2[] innerbuffer = ArrayPool<Vector2>.Shared.Rent(length);
            try
            {
                int count = this.Shape.FindIntersections(
                    start,
                    end,
                    innerbuffer,
                    length,
                    0);

                for (var i = 0; i < count; i++)
                {
                    buffer[i + offset] = innerbuffer[i].Y;
                }

                return count;
            }
            finally
            {
                ArrayPool<Vector2>.Shared.Return(innerbuffer);
            }
        }

        /// <summary>
        /// Scans the Y axis for intersections.
        /// </summary>
        /// <param name="y">The position along the y axis to find intersections.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>
        /// The number of intersections found.
        /// </returns>
        public override int ScanY(int y, float[] buffer, int length, int offset)
        {
            var start = new Vector2(float.MinValue, y);
            var end = new Vector2(float.MaxValue, y);
            Vector2[] innerbuffer = ArrayPool<Vector2>.Shared.Rent(length);
            try
            {
                int count = this.Shape.FindIntersections(
                    start,
                    end,
                    innerbuffer,
                    length,
                    0);

                for (var i = 0; i < count; i++)
                {
                    buffer[i + offset] = innerbuffer[i].X;
                }

                return count;
            }
            finally
            {
                ArrayPool<Vector2>.Shared.Return(innerbuffer);
            }
        }
    }
}
