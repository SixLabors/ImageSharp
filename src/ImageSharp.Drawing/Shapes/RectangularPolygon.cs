// <copyright file="RectangularPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using Paths;

    /// <summary>
    /// A way of optermising drawing rectangles.
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Shapes.IShape" />
    public class RectangularPolygon : IShape, IPath
    {
        private readonly RectangleF rectangle;
        private readonly Vector2 topLeft;
        private readonly Vector2 bottomRight;
        private readonly Vector2[] points;
        private readonly IEnumerable<IPath> pathCollection;
        private readonly float halfLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="RectangularPolygon"/> class.
        /// </summary>
        /// <param name="rect">The rect.</param>
        public RectangularPolygon(ImageSharp.Rectangle rect)
            : this((RectangleF)rect)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RectangularPolygon"/> class.
        /// </summary>
        /// <param name="rect">The rect.</param>
        public RectangularPolygon(ImageSharp.RectangleF rect)
        {
            this.rectangle = rect;
            this.points = new Vector2[4]
            {
                this.topLeft = new Vector2(rect.Left, rect.Top),
                new Vector2(rect.Right, rect.Top),
                this.bottomRight = new Vector2(rect.Right, rect.Bottom),
                new Vector2(rect.Left, rect.Bottom)
            };

            this.halfLength = this.rectangle.Width + this.rectangle.Height;
            this.Length = this.halfLength * 2;
            this.pathCollection = new[] { this };
        }

        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds => this.rectangle;

        /// <summary>
        /// Gets a value indicating whether this instance is closed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is closed; otherwise, <c>false</c>.
        /// </value>
        public bool IsClosed => true;

        /// <summary>
        /// Gets the length of the path
        /// </summary>
        /// <value>
        /// The length.
        /// </value>
        public float Length { get; }

        /// <summary>
        /// Gets the maximum number intersections that a shape can have when testing a line.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        public int MaxIntersections => 4;

        /// <summary>
        /// Calculates the distance along and away from the path for a specified point.
        /// </summary>
        /// <param name="point">The point along the path.</param>
        /// <returns>
        /// Returns details about the point and its distance away from the path.
        /// </returns>
        PointInfo IPath.Distance(Vector2 point)
        {
            bool inside; // dont care about inside/outside for paths just distance
            return this.Distance(point, false, out inside);
        }

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// Returns the distance from the shape to the point
        /// </returns>
        public float Distance(Vector2 point)
        {
            bool insidePoly;
            PointInfo result = this.Distance(point, true, out insidePoly);

            // invert the distance from path when inside
            return insidePoly ? -result.DistanceFromPath : result.DistanceFromPath;
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
        /// Converts the <see cref="ILineSegment" /> into a simple linear path..
        /// </summary>
        /// <returns>
        /// Returns the current <see cref="ILineSegment" /> as simple linear path.
        /// </returns>
        public Vector2[] AsSimpleLinearPath()
        {
            return this.points;
        }

        /// <summary>
        /// Based on a line described by <paramref name="start"/> and <paramref name="end"/>
        /// populate a buffer for all points on the edges of the <see cref="RectangularPolygon"/>
        /// that the line intersects.
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
            int discovered = 0;
            Vector2 startPoint = Vector2.Clamp(start, this.topLeft, this.bottomRight);
            Vector2 endPoint = Vector2.Clamp(end, this.topLeft, this.bottomRight);

            if (startPoint == Vector2.Clamp(startPoint, start, end))
            {
                // if start closest is within line then its a valid point
                discovered++;
                buffer[offset++] = startPoint;
            }

            if (endPoint == Vector2.Clamp(endPoint, start, end))
            {
                // if start closest is within line then its a valid point
                discovered++;
                buffer[offset++] = endPoint;
            }

            return discovered;
        }

        private PointInfo Distance(Vector2 point, bool getDistanceAwayOnly, out bool isInside)
        {
            // point in rectangle
            // if after its clamped by the extreams its still the same then it must be inside :)
            Vector2 clamped = Vector2.Clamp(point, this.topLeft, this.bottomRight);
            isInside = clamped == point;

            float distanceFromEdge = float.MaxValue;
            float distanceAlongEdge = 0f;

            if (isInside)
            {
                // get the absolute distances from the extreams
                Vector2 topLeftDist = Vector2.Abs(point - this.topLeft);
                Vector2 bottomRightDist = Vector2.Abs(point - this.bottomRight);

                // get the min components
                Vector2 minDists = Vector2.Min(topLeftDist, bottomRightDist);

                // and then the single smallest (dont have to worry about direction)
                distanceFromEdge = Math.Min(minDists.X, minDists.Y);

                if (!getDistanceAwayOnly)
                {
                    // we need to make clamped the closest point
                    if (this.topLeft.X + distanceFromEdge == point.X)
                    {
                        // closer to lhf
                        clamped.X = this.topLeft.X; // y is already the same

                        // distance along edge is length minus the amout down we are from the top of the rect
                        distanceAlongEdge = this.Length - (clamped.Y - this.topLeft.Y);
                    }
                    else if (this.topLeft.Y + distanceFromEdge == point.Y)
                    {
                        // closer to top
                        clamped.Y = this.topLeft.Y; // x is already the same

                        distanceAlongEdge = clamped.X - this.topLeft.X;
                    }
                    else if (this.bottomRight.Y - distanceFromEdge == point.Y)
                    {
                        // closer to bottom
                        clamped.Y = this.bottomRight.Y; // x is already the same

                        distanceAlongEdge = (this.bottomRight.X - clamped.X) + this.halfLength;
                    }
                    else if (this.bottomRight.X - distanceFromEdge == point.X)
                    {
                        // closer to rhs
                        clamped.X = this.bottomRight.X; // x is already the same

                        distanceAlongEdge = (this.bottomRight.Y - clamped.Y) + this.rectangle.Width;
                    }
                }
            }
            else
            {
                // clamped is the point on the path thats closest no matter what
                distanceFromEdge = (clamped - point).Length();

                if (!getDistanceAwayOnly)
                {
                    // we need to figure out whats the cloests edge now and thus what distance/poitn is closest
                    if (this.topLeft.X == clamped.X)
                    {
                        // distance along edge is length minus the amout down we are from the top of the rect
                        distanceAlongEdge = this.Length - (clamped.Y - this.topLeft.Y);
                    }
                    else if (this.topLeft.Y == clamped.Y)
                    {
                        distanceAlongEdge = clamped.X - this.topLeft.X;
                    }
                    else if (this.bottomRight.Y == clamped.Y)
                    {
                        distanceAlongEdge = (this.bottomRight.X - clamped.X) + this.halfLength;
                    }
                    else if (this.bottomRight.X == clamped.X)
                    {
                        distanceAlongEdge = (this.bottomRight.Y - clamped.Y) + this.rectangle.Width;
                    }
                }
            }

            return new PointInfo
            {
                SearchPoint = point,
                DistanceFromPath = distanceFromEdge,
                ClosestPointOnPath = clamped,
                DistanceAlongPath = distanceAlongEdge
            };
        }
    }
}
