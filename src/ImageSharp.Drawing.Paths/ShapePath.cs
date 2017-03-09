// <copyright file="ShapePath.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    using System.Buffers;
    using System.Collections.Immutable;
    using System.Numerics;

    using SixLabors.Shapes;

    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// A drawable mapping between a <see cref="IPath"/> and a drawable region.
    /// </summary>
    internal class ShapePath : Drawable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapePath"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public ShapePath(IPath path)
        {
            this.Path = path;
            this.Bounds = path.Bounds.Convert();
        }

        /// <summary>
        /// Gets the fillable shape
        /// </summary>
        public IPath Path { get; }

        /// <inheritdoc/>
        public override int MaxIntersections => this.Path.MaxIntersections;

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
                int count = this.Path.FindIntersections(start, end, innerbuffer, length, 0);

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
            Vector2 start = new Vector2(this.Bounds.Left - 1, y);
            Vector2 end = new Vector2(this.Bounds.Right + 1, y);
            Vector2[] innerbuffer = ArrayPool<Vector2>.Shared.Rent(length);
            try
            {
                int count = this.Path.FindIntersections(start, end, innerbuffer, length, 0);

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
            SixLabors.Shapes.PointInfo dist = this.Path.Distance(point);

            return new PointInfo
            {
                DistanceAlongPath = dist.DistanceAlongPath,
                DistanceFromPath =
                               dist.DistanceFromPath < 0
                                   ? -dist.DistanceFromPath
                                   : dist.DistanceFromPath
            };
        }
    }
}
