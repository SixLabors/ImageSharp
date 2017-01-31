// <copyright file="ShapePath.cs" company="James Jackson-South">
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
    internal class ShapePath : ImageSharp.Drawing.Path
    {
        /// <summary>
        /// The fillable shape
        /// </summary>
        private readonly IShape shape;

        /// <summary>
        /// The drawable paths
        /// </summary>
        private readonly ImmutableArray<IPath> paths;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShapePath"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public ShapePath(IPath path)
            : this(ImmutableArray.Create(path))
        {
            this.shape = path.AsShape();
            this.Bounds = RectangleF.Ceiling(path.Bounds.Convert());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShapePath"/> class.
        /// </summary>
        /// <param name="shape">The shape.</param>
        public ShapePath(IShape shape)
            : this(shape.Paths)
        {
            this.shape = shape;
            this.Bounds = RectangleF.Ceiling(shape.Bounds.Convert());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShapePath" /> class.
        /// </summary>
        /// <param name="paths">The paths.</param>
        private ShapePath(ImmutableArray<IPath> paths)
        {
            this.paths = paths;
        }

        /// <summary>
        /// Gets the maximum number of intersections to could be returned.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        public override int MaxIntersections => this.shape.MaxIntersections;

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
                int count = this.shape.FindIntersections(
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
                int count = this.shape.FindIntersections(
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

        /// <summary>
        /// Gets the point information for the specified x and y location.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>Information about the the point</returns>
        public override PointInfo GetPointInfo(int x, int y)
        {
            var point = new Vector2(x, y);
            SixLabors.Shapes.PointInfo result = default(SixLabors.Shapes.PointInfo);
            float distance = float.MaxValue;

            for (int i = 0; i < this.paths.Length; i++)
            {
                var p = this.paths[i].Distance(point);
                if (p.DistanceFromPath < distance)
                {
                    distance = p.DistanceFromPath;
                    result = p;
                }
            }

            return result.Convert();
        }
    }
}
