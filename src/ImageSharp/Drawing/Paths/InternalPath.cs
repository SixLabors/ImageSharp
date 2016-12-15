// <copyright file="InternalPath.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Internal logic for interigating linear paths.
    /// </summary>
    internal class InternalPath
    {
        private readonly Vector2[] points;
        private readonly bool closedPath;
        private readonly Lazy<float> totalDistance;

        private float[] constant;
        private float[] multiple;
        private float[] distance;
        private object locker = new object();
        private bool calculated = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalPath"/> class.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="isClosedPath">if set to <c>true</c> [is closed path].</param>
        internal InternalPath(ILineSegment[] segments, bool isClosedPath)
        {
            Guard.NotNull(segments, nameof(segments));

            this.points = this.Simplify(segments);
            this.closedPath = isClosedPath;

            var minX = this.points.Min(x => x.X);
            var maxX = this.points.Max(x => x.X);
            var minY = this.points.Min(x => x.Y);
            var maxY = this.points.Max(x => x.Y);

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

            var internalInfo = default(PointInfoInternal);
            internalInfo.DistanceSquared = float.MaxValue; // set it to max so that CalculateShorterDistance can reduce it back down

            var polyCorners = this.points.Length;

            if (!this.closedPath)
            {
                polyCorners -= 1;
            }

            int closestPoint = 0;
            for (var i = 0; i < polyCorners; i++)
            {
                var next = i + 1;
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
        /// Points the in polygon.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>Returns true if the point is inside the closed path.</returns>
        public bool PointInPolygon(Vector2 point)
        {
            // you can only be inside a path if its "closed"
            if (!this.closedPath)
            {
                return false;
            }

            if (!this.Bounds.Contains(point.X, point.Y))
            {
                return false;
            }

            this.CalculateConstants();

            var poly = this.points;
            var polyCorners = poly.Length;

            var j = polyCorners - 1;
            bool oddNodes = false;

            for (var i = 0; i < polyCorners; i++)
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

        private Vector2[] Simplify(ILineSegment[] segments)
        {
            List<Vector2> points = new List<Vector2>();
            foreach(var seg in segments)
            {
                points.AddRange(seg.AsSimpleLinearPath());
            }

            return points.ToArray();
        }

        private float CalculateLength()
        {
            float length = 0;
            var polyCorners = this.points.Length;

            if (!this.closedPath)
            {
                polyCorners -= 1;
            }

            for (var i = 0; i < polyCorners; i++)
            {
                var next = i + 1;
                if (this.closedPath && next == polyCorners)
                {
                    next = 0;
                }

                length += Vector2.Distance(this.points[i], this.points[next]);
            }

            return length;
        }

        private void CalculateConstants()
        {
            // http://alienryderflex.com/polygon/ source for point in polygon logic
            if (this.calculated)
            {
                return;
            }

            lock (this.locker)
            {
                if (this.calculated)
                {
                    return;
                }

                var poly = this.points;
                var polyCorners = poly.Length;
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
                        var subtracted = poly[j] - poly[i];
                        this.constant[i] = (poly[i].X - ((poly[i].Y * poly[j].X) / subtracted.Y)) + ((poly[i].Y * poly[i].X) / subtracted.Y);
                        this.multiple[i] = subtracted.X / subtracted.Y;
                    }

                    j = i;
                }

                this.calculated = true;
            }
        }

        private bool CalculateShorterDistance(Vector2 start, Vector2 end, Vector2 point, ref PointInfoInternal info)
        {
            var diffEnds = end - start;

            float lengthSquared = diffEnds.LengthSquared();
            var diff = point - start;

            var multiplied = diff * diffEnds;
            var u = (multiplied.X + multiplied.Y) / lengthSquared;

            if (u > 1)
            {
                u = 1;
            }
            else if (u < 0)
            {
                u = 0;
            }

            var multipliedByU = diffEnds * u;

            var pointOnLine = start + multipliedByU;

            var d = pointOnLine - point;

            var dist = d.LengthSquared();

            if (info.DistanceSquared > dist)
            {
                info.DistanceSquared = dist;
                info.PointOnLine = pointOnLine;
                return true;
            }

            return false;
        }

        private struct PointInfoInternal
        {
            public float DistanceSquared;
            public Vector2 PointOnLine;
        }
    }
}
