// <copyright file="Polygon.cs" company="James Jackson-South">
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
    /// A shape made up of a single path made up of one of more <see cref="ILineSegment"/>s
    /// </summary>
    public sealed class Polygon : IShape, IPath
    {
        private readonly InternalPath innerPath;
        private readonly IEnumerable<IPath> pathCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="Polygon"/> class.
        /// </summary>
        /// <param name="segments">The segments.</param>
        public Polygon(params ILineSegment[] segments)
            : this((IEnumerable<ILineSegment>)segments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Polygon"/> class.
        /// </summary>
        /// <param name="segments">The segments.</param>
        public Polygon(IEnumerable<ILineSegment> segments)
        {
            this.innerPath = new InternalPath(segments, true);
            this.pathCollection = new[] { this };
        }

        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds => this.innerPath.Bounds;

        /// <summary>
        /// Gets the length of the path
        /// </summary>
        /// <value>
        /// The length.
        /// </value>
        public float Length => this.innerPath.Length;

        /// <summary>
        /// Gets a value indicating whether this instance is closed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is closed; otherwise, <c>false</c>.
        /// </value>
        public bool IsClosed => true;

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        /// The distance of the point away from the shape
        /// </returns>
        public float Distance(int x, int y)
        {
            var point = new Vector2(x, y);

            bool isInside = this.innerPath.PointInPolygon(point);

            var distance = this.innerPath.DistanceFromPath(point).DistanceFromPath;
            if (isInside)
            {
                return -distance;
            }

            return distance;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<IPath> GetEnumerator()
        {
            return this.pathCollection.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.pathCollection.GetEnumerator();
        }

        /// <summary>
        /// Calcualtes the distance along and away from the path for a specified point.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        /// distance metadata about the point.
        /// </returns>
        PointInfo IPath.Distance(int x, int y)
        {
            return this.innerPath.DistanceFromPath(new Vector2(x, y));
        }

        /// <summary>
        /// Returns the current shape as a simple linear path.
        /// </summary>
        /// <returns>
        /// Returns the current <see cref="ILineSegment" /> as simple linear path.
        /// </returns>
        public Vector2[] AsSimpleLinearPath()
        {
            return this.innerPath.Points;
        }
    }
}
