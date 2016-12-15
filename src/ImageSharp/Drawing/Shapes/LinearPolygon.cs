// <copyright file="LinearPolygon.cs" company="James Jackson-South">
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
    /// Represents a polygon made up exclusivly of a single Linear path.
    /// </summary>
    public sealed class LinearPolygon : IShape
    {
        private Polygon innerPolygon;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearPolygon"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public LinearPolygon(params Vector2[] points)
        {
            this.innerPolygon = new Polygon(new LinearLineSegment(points));
        }
        
        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds => this.innerPolygon.Bounds;

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        /// Returns the distance from the shape to the point
        /// </returns>
        public float Distance(int x, int y) => this.innerPolygon.Distance(x, y);

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<IPath> GetEnumerator() => this.innerPolygon.GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => this.innerPolygon.GetEnumerator();
    }
}
