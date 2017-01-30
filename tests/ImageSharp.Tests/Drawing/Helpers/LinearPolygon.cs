// <copyright file="LinearPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.Shapes
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Numerics;

    using SixLabors.Shapes;
    public class LinearPolygon : IShape
    {
        private Polygon polygon;

        public LinearPolygon(params Vector2[] points)
        {
            this.polygon = new Polygon(new LinearLineSegment(points));
        }

        public float Distance(Vector2 point)
        {
            return this.polygon.Distance(point);
        }

        public bool Contains(Vector2 point)
        {
            return this.polygon.Contains(point);
        }

        public int FindIntersections(Vector2 start, Vector2 end, Vector2[] buffer, int count, int offset)
        {
            return this.polygon.FindIntersections(start, end, buffer, count, offset);
        }

        public IEnumerable<Vector2> FindIntersections(Vector2 start, Vector2 end)
        {
            return this.polygon.FindIntersections(start, end);
        }

        public IShape Transform(Matrix3x2 matrix)
        {
            return ((IShape)this.polygon).Transform(matrix);
        }

        public Rectangle Bounds
        {
            get
            {
                return this.polygon.Bounds;
            }
        }

        public ImmutableArray<IPath> Paths
        {
            get
            {
                return this.polygon.Paths;
            }
        }

        public int MaxIntersections
        {
            get
            {
                return this.polygon.MaxIntersections;
            }
        }
    }
}
