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
        /// The locker.
        /// </summary>
        private static readonly object Locker = new object();

        /// <summary>
        /// The offset
        /// </summary>
        private readonly Vector2 offset;

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
            : this(points, isClosedPath, Vector2.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath" /> class.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        /// <param name="offset">The offset.</param>
        internal InternalPath(Vector2[] points, bool isClosedPath, Vector2 offset)
        {
            this.offset = offset;
            this.points = points;
            this.closedPath = isClosedPath;

            float minX = this.points.Min(x => x.X);
            float maxX = this.points.Max(x => x.X);
            float minY = this.points.Min(x => x.Y);
            float maxY = this.points.Max(x => x.Y);

            this.Bounds = new RectangleF(minX + offset.X, minY + offset.Y, maxX - minX, maxY - minY);
            this.totalDistance = new Lazy<float>(this.CalculateLength);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath" /> class.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        /// <param name="offset">The offset.</param>
        internal InternalPath(InternalPath path, bool isClosedPath,  Vector2 offset)
            : this(path.points,  isClosedPath, offset)
        {
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
            var initalPoint = point;
            point = point - this.offset;
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
                SearchPoint = initalPoint,
                ClosestPointOnPath = internalInfo.PointOnLine + this.offset
            };
        }

        /// <summary>
        /// Points the in polygon.
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

            // The bounding box is already offset so check against origional point
            if (!this.Bounds.Contains(point.X, point.Y))
            {
                return false;
            }

            // Ofset the checked point into the smae range as the points
            point = point - this.offset;

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
