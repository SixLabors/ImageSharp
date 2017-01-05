// <copyright file="InternalPath.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Drawing.Paths
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Internal logic for integrating linear paths.
    /// </summary>
    internal class InternalPath
    {
        /// <summary>
        /// The maximum vector
        /// </summary>
        private static readonly Vector2 MaxVector = new Vector2(float.MaxValue);

        /// <summary>
        /// The locker.
        /// </summary>
        private static readonly object Locker = new object();

        /// <summary>
        /// The points.
        /// </summary>
        private readonly Vector2[] points;

        /// <summary>
        /// The closed path.
        /// </summary>
        private readonly bool closedPath;

        /// <summary>
        /// The total distance.
        /// </summary>
        private readonly Lazy<float> totalDistance;

        /// <summary>
        /// The constant.
        /// </summary>
        private float[] constant;

        /// <summary>
        /// The multiples.
        /// </summary>
        private float[] multiple;

        /// <summary>
        /// The distances.
        /// </summary>
        private float[] distance;

        /// <summary>
        /// The calculated.
        /// </summary>
        private bool calculated = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath"/> class.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        internal InternalPath(ILineSegment[] segments, bool isClosedPath)
            : this(Simplify(segments), isClosedPath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath" /> class.
        /// </summary>
        /// <param name="segment">The segment.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        internal InternalPath(ILineSegment segment, bool isClosedPath)
            : this(segment.AsSimpleLinearPath(), isClosedPath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath" /> class.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        internal InternalPath(Vector2[] points, bool isClosedPath)
        {
            this.points = points;
            this.closedPath = isClosedPath;

            float minX = this.points.Min(x => x.X);
            float maxX = this.points.Max(x => x.X);
            float minY = this.points.Min(x => x.Y);
            float maxY = this.points.Max(x => x.Y);

            this.Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            this.totalDistance = new Lazy<float>(this.CalculateLength);
        }

        /// <summary>
        /// Gets the bounds.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds
        {
            get;
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <value>
        /// The length.
        /// </value>
        public float Length => this.totalDistance.Value;

        /// <summary>
        /// Gets the points.
        /// </summary>
        /// <value>
        /// The points.
        /// </value>
        internal Vector2[] Points => this.points;

        /// <summary>
        /// Calculates the distance from the path.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>Returns the distance from the path</returns>
        public PointInfo DistanceFromPath(Vector2 point)
        {
            this.CalculateConstants();

            PointInfoInternal internalInfo = default(PointInfoInternal);
            internalInfo.DistanceSquared = float.MaxValue; // Set it to max so that CalculateShorterDistance can reduce it back down

            int polyCorners = this.points.Length;

            if (!this.closedPath)
            {
                polyCorners -= 1;
            }

            int closestPoint = 0;
            for (int i = 0; i < polyCorners; i++)
            {
                int next = i + 1;
                if (this.closedPath && next == polyCorners)
                {
                    next = 0;
                }

                if (this.CalculateShorterDistance(this.points[i], this.points[next], point, ref internalInfo))
                {
                    closestPoint = i;
                }
            }

            return new PointInfo
            {
                DistanceAlongPath = this.distance[closestPoint] + Vector2.Distance(this.points[closestPoint], point),
                DistanceFromPath = (float)Math.Sqrt(internalInfo.DistanceSquared),
                SearchPoint = point,
                ClosestPointOnPath = internalInfo.PointOnLine
            };
        }

        /// <summary>
        /// Based on a line described by <paramref name="start" /> and <paramref name="end" />
        /// populate a buffer for all points on the path that the line intersects.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="count">The count.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>number iof intersections hit</returns>
        public int FindIntersections(Vector2 start, Vector2 end, Vector2[] buffer, int count, int offset)
        {
            int polyCorners = this.points.Length;

            if (!this.closedPath)
            {
                polyCorners -= 1;
            }

            int position = 0;
            for (int i = 0; i < polyCorners && count > 0; i++)
            {
                int next = i + 1;
                if (this.closedPath && next == polyCorners)
                {
                    next = 0;
                }

                Vector2 point = FindIntersection(this.points[i], this.points[next], start, end);
                if (point != MaxVector)
                {
                    buffer[position + offset] = point;
                    position++;
                    count--;
                }
            }

            return position;
        }

        /// <summary>
        /// Determines if the specified point is inside or outside the path.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>Returns true if the point is inside the closed path.</returns>
        public bool PointInPolygon(Vector2 point)
        {
            // You can only be inside a path if its "closed"
            if (!this.closedPath)
            {
                return false;
            }

            if (!this.Bounds.Contains(point.X, point.Y))
            {
                return false;
            }

            this.CalculateConstants();

            Vector2[] poly = this.points;
            int polyCorners = poly.Length;

            int j = polyCorners - 1;
            bool oddNodes = false;

            for (int i = 0; i < polyCorners; i++)
            {
                if ((poly[i].Y < point.Y && poly[j].Y >= point.Y)
                || (poly[j].Y < point.Y && poly[i].Y >= point.Y))
                {
                    oddNodes ^= (point.Y * this.multiple[i]) + this.constant[i] < point.X;
                }

                j = i;
            }

            return oddNodes;
        }

        /// <summary>
        /// Determins if the bounding box for 2 lines
        /// described by <paramref name="line1Start" /> and <paramref name="line1End" />
        /// and  <paramref name="line2Start" /> and <paramref name="line2End" /> overlap.
        /// </summary>
        /// <param name="line1Start">The line1 start.</param>
        /// <param name="line1End">The line1 end.</param>
        /// <param name="line2Start">The line2 start.</param>
        /// <param name="line2End">The line2 end.</param>
        /// <returns>Returns true it the bounding box of the 2 lines intersect</returns>
        private static bool BoundingBoxesIntersect(Vector2 line1Start, Vector2 line1End, Vector2 line2Start, Vector2 line2End)
        {
            Vector2 topLeft1 = Vector2.Min(line1Start, line1End);
            Vector2 bottomRight1 = Vector2.Max(line1Start, line1End);

            Vector2 topLeft2 = Vector2.Min(line2Start, line2End);
            Vector2 bottomRight2 = Vector2.Max(line2Start, line2End);

            float left1 = topLeft1.X;
            float right1 = bottomRight1.X;
            float top1 = topLeft1.Y;
            float bottom1 = bottomRight1.Y;

            float left2 = topLeft2.X;
            float right2 = bottomRight2.X;
            float top2 = topLeft2.Y;
            float bottom2 = bottomRight2.Y;

            return left1 <= right2 && right1 >= left2
                &&
                top1 <= bottom2 && bottom1 >= top2;
        }

        /// <summary>
        /// Finds the point on line described by <paramref name="line1Start" /> and <paramref name="line1End" />
        /// that intersects with line described by <paramref name="line2Start" /> and <paramref name="line2End" />
        /// </summary>
        /// <param name="line1Start">The line1 start.</param>
        /// <param name="line1End">The line1 end.</param>
        /// <param name="line2Start">The line2 start.</param>
        /// <param name="line2End">The line2 end.</param>
        /// <returns>
        /// A <see cref="Vector2"/> describing the point that the 2 lines cross or <see cref="MaxVector"/> if they do not.
        /// </returns>
        private static Vector2 FindIntersection(Vector2 line1Start, Vector2 line1End, Vector2 line2Start, Vector2 line2End)
        {
            // do bounding boxes overlap, if not then the lines can't and return fast.
            if (!BoundingBoxesIntersect(line1Start, line1End, line2Start, line2End))
            {
                return MaxVector;
            }

            Vector2 line1Diff = line1End - line1Start;
            Vector2 line2Diff = line2End - line2Start;

            Vector2 point;
            if (line1Diff.X == 0)
            {
                float slope = line2Diff.Y / line2Diff.X;
                float yinter = line2Start.Y - (slope * line2Start.X);
                float y = (line1Start.X * slope) + yinter;
                point = new Vector2(line1Start.X, y);

                // horizontal and vertical lines
            }
            else if (line2Diff.X == 0)
            {
                float slope = line1Diff.Y / line1Diff.X;
                float yinter = line1Start.Y - (slope * line1Start.X);
                float y = (line2Start.X * slope) + yinter;
                point = new Vector2(line2Start.X, y);

                // horizontal and vertical lines
            }
            else
            {
                float slope1 = line1Diff.Y / line1Diff.X;
                float slope2 = line2Diff.Y / line2Diff.X;

                float yinter1 = line1Start.Y - (slope1 * line1Start.X);
                float yinter2 = line2Start.Y - (slope2 * line2Start.X);

                if (slope1 == slope2 && yinter1 != yinter2)
                {
                    return MaxVector;
                }

                float x = (yinter2 - yinter1) / (slope1 - slope2);
                float y = (slope1 * x) + yinter1;

                point = new Vector2(x, y);
            }

            if (BoundingBoxesIntersect(line1Start, line1End, point, point))
            {
                return point;
            }
            else if (BoundingBoxesIntersect(line2Start, line2End, point, point))
            {
                return point;
            }

            return MaxVector;
        }

        /// <summary>
        /// Simplifies the collection of segments.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <returns>
        /// The <see cref="T:Vector2[]"/>.
        /// </returns>
        private static Vector2[] Simplify(ILineSegment[] segments)
        {
            List<Vector2> simplified = new List<Vector2>();
            foreach (ILineSegment seg in segments)
            {
                simplified.AddRange(seg.AsSimpleLinearPath());
            }

            return simplified.ToArray();
        }

        /// <summary>
        /// Returns the length of the path.
        /// </summary>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private float CalculateLength()
        {
            float length = 0;
            int polyCorners = this.points.Length;

            if (!this.closedPath)
            {
                polyCorners -= 1;
            }

            for (int i = 0; i < polyCorners; i++)
            {
                int next = i + 1;
                if (this.closedPath && next == polyCorners)
                {
                    next = 0;
                }

                length += Vector2.Distance(this.points[i], this.points[next]);
            }

            return length;
        }

        /// <summary>
        /// Calculate the constants.
        /// </summary>
        private void CalculateConstants()
        {
            // http://alienryderflex.com/polygon/ source for point in polygon logic
            if (this.calculated)
            {
                return;
            }

            lock (Locker)
            {
                if (this.calculated)
                {
                    return;
                }

                Vector2[] poly = this.points;
                int polyCorners = poly.Length;
                this.constant = new float[polyCorners];
                this.multiple = new float[polyCorners];
                this.distance = new float[polyCorners];
                int i, j = polyCorners - 1;

                this.distance[0] = 0;

                for (i = 0; i < polyCorners; i++)
                {
                    this.distance[j] = this.distance[i] + Vector2.Distance(poly[i], poly[j]);
                    if (poly[j].Y == poly[i].Y)
                    {
                        this.constant[i] = poly[i].X;
                        this.multiple[i] = 0;
                    }
                    else
                    {
                        Vector2 subtracted = poly[j] - poly[i];
                        this.constant[i] = (poly[i].X - ((poly[i].Y * poly[j].X) / subtracted.Y)) + ((poly[i].Y * poly[i].X) / subtracted.Y);
                        this.multiple[i] = subtracted.X / subtracted.Y;
                    }

                    j = i;
                }

                this.calculated = true;
            }
        }

        /// <summary>
        /// Calculate any shorter distances along the path.
        /// </summary>
        /// <param name="start">The start position.</param>
        /// <param name="end">The end position.</param>
        /// <param name="point">The current point.</param>
        /// <param name="info">The info.</param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool CalculateShorterDistance(Vector2 start, Vector2 end, Vector2 point, ref PointInfoInternal info)
        {
            Vector2 diffEnds = end - start;

            float lengthSquared = diffEnds.LengthSquared();
            Vector2 diff = point - start;

            Vector2 multiplied = diff * diffEnds;
            float u = (multiplied.X + multiplied.Y) / lengthSquared;

            if (u > 1)
            {
                u = 1;
            }
            else if (u < 0)
            {
                u = 0;
            }

            Vector2 multipliedByU = diffEnds * u;

            Vector2 pointOnLine = start + multipliedByU;

            Vector2 d = pointOnLine - point;

            float dist = d.LengthSquared();

            if (info.DistanceSquared > dist)
            {
                info.DistanceSquared = dist;
                info.PointOnLine = pointOnLine;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Contains information about the current point.
        /// </summary>
        private struct PointInfoInternal
        {
            /// <summary>
            /// The distance squared.
            /// </summary>
            public float DistanceSquared;

            /// <summary>
            /// The point on the current line.
            /// </summary>
            public Vector2 PointOnLine;
        }
    }
}
