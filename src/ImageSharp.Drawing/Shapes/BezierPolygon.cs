// <copyright file="BezierPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Numerics;
    using Paths;

    /// <summary>
    /// Represents a polygon made up exclusivly of a single close cubic Bezier curve.
    /// </summary>
    public sealed class BezierPolygon : IShape
    {
        private Polygon innerPolygon;

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierPolygon"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public BezierPolygon(params Vector2[] points)
        {
            this.innerPolygon = new Polygon(new BezierLineSegment(points));
        }

        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds => this.innerPolygon.Bounds;

        /// <summary>
        /// Gets the maximum number intersections that a shape can have when testing a line.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        public int MaxIntersections => this.innerPolygon.MaxIntersections;

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The distance from the shape.
        /// </returns>
        public float Distance(Vector2 point) => this.innerPolygon.Distance(point);

        /// <summary>
        /// Based on a line described by <paramref name="start"/> and <paramref name="end"/>
        /// populate a buffer for all points on the polygon that the line intersects.
        /// </summary>
        /// <param name="start">The start point of the line.</param>
        /// <param name="end">The end point of the line.</param>
        /// <param name="buffer">The buffer that will be populated with intersections.</param>
        /// <param name="count">The count.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>
        /// The number of intersections populated into the buffer.
        /// </returns>
        public int FindIntersections(Vector2 start, Vector2 end, Vector2[] buffer, int count, int offset)
        {
            return this.innerPolygon.FindIntersections(start, end, buffer, count, offset);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<IPath> GetEnumerator()
        {
            return this.innerPolygon.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.innerPolygon.GetEnumerator();
        }
    }
}
