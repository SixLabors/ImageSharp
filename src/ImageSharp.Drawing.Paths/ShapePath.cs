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
    internal class ShapePath : ImageSharp.Drawing.Drawable
    {
        /// <summary>
        /// The fillable shape
        /// </summary>
        private readonly IShape shape;

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
            this.Paths = paths;
        }

        /// <summary>
        /// Gets the drawable paths
        /// </summary>
        /// <value>
        /// The paths.
        /// </value>
        public ImmutableArray<IPath> Paths { get; }

        /// <inheritdoc/>
        public override int MaxIntersections => this.shape.MaxIntersections;

        /// <inheritdoc/>
        public override Rectangle Bounds { get; }

        /// <inheritdoc/>
        public override int ScanX(int x, float[] buffer, int length, int offset)
        {
            Vector2 start = new Vector2(x, this.Bounds.Top - 1);
            Vector2 end = new Vector2(x, this.Bounds.Bottom + 1);
            Vector2[] innerbuffer = ArrayPool<Vector2>.Shared.Rent(length);
            try
            {
                int count = this.shape.FindIntersections(
                    start,
                    end,
                    innerbuffer,
                    length,
                    0);

                for (int i = 0; i < count; i++)
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

        /// <inheritdoc/>
        public override int ScanY(int y, float[] buffer, int length, int offset)
        {
            Vector2 start = new Vector2(float.MinValue, y);
            Vector2 end = new Vector2(float.MaxValue, y);
            Vector2[] innerbuffer = ArrayPool<Vector2>.Shared.Rent(length);
            try
            {
                int count = this.shape.FindIntersections(
                    start,
                    end,
                    innerbuffer,
                    length,
                    0);

                for (int i = 0; i < count; i++)
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

        /// <inheritdoc/>
        public override PointInfo GetPointInfo(int x, int y)
        {
            Vector2 point = new Vector2(x, y);
            float distanceFromPath = float.MaxValue;
            float distanceAlongPath = 0;

            for (int i = 0; i < this.Paths.Length; i++)
            {
                SixLabors.Shapes.PointInfo p = this.Paths[i].Distance(point);
                if (p.DistanceFromPath < distanceFromPath)
                {
                    distanceFromPath = p.DistanceFromPath;
                    distanceAlongPath = p.DistanceAlongPath;
                }
            }

            return new PointInfo
            {
                DistanceAlongPath = distanceAlongPath,
                DistanceFromPath = distanceFromPath
            };
        }
    }
}
