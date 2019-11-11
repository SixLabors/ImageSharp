// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides an implementation of a brush for painting gradients between multiple color positions in 2D coordinates.
    /// It works similarly with the class in System.Drawing.Drawing2D of the same name.
    /// </summary>
    public sealed class PathGradientBrush : IBrush
    {
        private readonly IList<Edge> edges;

        private readonly Color centerColor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathGradientBrush"/> class.
        /// </summary>
        /// <param name="points">Points that constitute a polygon that represents the gradient area.</param>
        /// <param name="colors">Array of colors that correspond to each point in the polygon.</param>
        /// <param name="centerColor">Color at the center of the gradient area to which the other colors converge.</param>
        public PathGradientBrush(PointF[] points, Color[] colors, Color centerColor)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Length < 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(points),
                    "There must be at least 3 lines to construct a path gradient brush.");
            }

            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (!colors.Any())
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colors),
                    "One or more color is needed to construct a path gradient brush.");
            }

            int size = points.Length;

            var lines = new ILineSegment[size];

            for (int i = 0; i < size; i++)
            {
                lines[i] = new LinearLineSegment(points[i % size], points[(i + 1) % size]);
            }

            this.centerColor = centerColor;

            Color ColorAt(int index) => colors[index % colors.Length];

            this.edges = lines.Select(s => new Path(s))
                .Select((path, i) => new Edge(path, ColorAt(i), ColorAt(i + 1))).ToList();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathGradientBrush"/> class.
        /// </summary>
        /// <param name="points">Points that constitute a polygon that represents the gradient area.</param>
        /// <param name="colors">Array of colors that correspond to each point in the polygon.</param>
        public PathGradientBrush(PointF[] points, Color[] colors)
            : this(points, colors, CalculateCenterColor(colors))
        {
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator<TPixel>(
            Configuration configuration,
            GraphicsOptions options,
            ImageFrame<TPixel> source,
            RectangleF region)
            where TPixel : struct, IPixel<TPixel>
        {
            return new PathGradientBrushApplicator<TPixel>(configuration, options, source, this.edges, this.centerColor);
        }

        private static Color CalculateCenterColor(Color[] colors)
        {
            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (!colors.Any())
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colors),
                    "One or more color is needed to construct a path gradient brush.");
            }

            return new Color(colors.Select(c => (Vector4)c).Aggregate((p1, p2) => p1 + p2) / colors.Length);
        }

        private static float DistanceBetween(PointF p1, PointF p2) => ((Vector2)(p2 - p1)).Length();

        private struct Intersection
        {
            public Intersection(PointF point, float distance)
            {
                this.Point = point;
                this.Distance = distance;
            }

            public PointF Point { get; }

            public float Distance { get; }
        }

        /// <summary>
        /// An edge of the polygon that represents the gradient area.
        /// </summary>
        private class Edge
        {
            private readonly Path path;

            private readonly float length;

            private readonly PointF[] buffer;

            public Edge(Path path, Color startColor, Color endColor)
            {
                this.path = path;

                Vector2[] points = path.LineSegments.SelectMany(s => s.Flatten()).Select(p => (Vector2)p).ToArray();

                this.Start = points.First();
                this.StartColor = (Vector4)startColor;

                this.End = points.Last();
                this.EndColor = (Vector4)endColor;

                this.length = DistanceBetween(this.End, this.Start);
                this.buffer = new PointF[this.path.MaxIntersections];
            }

            public PointF Start { get; }

            public Vector4 StartColor { get; }

            public PointF End { get; }

            public Vector4 EndColor { get; }

            public Intersection? FindIntersection(PointF start, PointF end)
            {
                int intersections = this.path.FindIntersections(start, end, this.buffer);

                if (intersections == 0)
                {
                    return null;
                }

                return this.buffer.Take(intersections)
                    .Select(p => new Intersection(point: p, distance: ((Vector2)(p - start)).LengthSquared()))
                    .Aggregate((min, current) => min.Distance > current.Distance ? current : min);
            }

            public Vector4 ColorAt(float distance)
            {
                float ratio = this.length > 0 ? distance / this.length : 0;

                return Vector4.Lerp(this.StartColor, this.EndColor, ratio);
            }

            public Vector4 ColorAt(PointF point) => this.ColorAt(DistanceBetween(point, this.Start));
        }

        /// <summary>
        /// The path gradient brush applicator.
        /// </summary>
        private class PathGradientBrushApplicator<TPixel> : BrushApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            private readonly PointF center;

            private readonly Vector4 centerColor;

            private readonly float maxDistance;

            private readonly IList<Edge> edges;

            /// <summary>
            /// Initializes a new instance of the <see cref="PathGradientBrushApplicator{TPixel}"/> class.
            /// </summary>
            /// <param name="configuration">The configuration instance to use when performing operations.</param>
            /// <param name="options">The graphics options.</param>
            /// <param name="source">The source image.</param>
            /// <param name="edges">Edges of the polygon.</param>
            /// <param name="centerColor">Color at the center of the gradient area to which the other colors converge.</param>
            public PathGradientBrushApplicator(
                Configuration configuration,
                GraphicsOptions options,
                ImageFrame<TPixel> source,
                IList<Edge> edges,
                Color centerColor)
                : base(configuration, options, source)
            {
                this.edges = edges;

                PointF[] points = edges.Select(s => s.Start).ToArray();

                this.center = points.Aggregate((p1, p2) => p1 + p2) / edges.Count;
                this.centerColor = (Vector4)centerColor;

                this.maxDistance = points.Select(p => (Vector2)(p - this.center)).Select(d => d.Length()).Max();
            }

            /// <inheritdoc />
            internal override TPixel this[int x, int y]
            {
                get
                {
                    var point = new PointF(x, y);

                    if (point == this.center)
                    {
                        return new Color(this.centerColor).ToPixel<TPixel>();
                    }

                    var direction = Vector2.Normalize(point - this.center);

                    PointF end = point + (PointF)(direction * this.maxDistance);

                    (Edge edge, Intersection? info) = this.FindIntersection(point, end);

                    if (!info.HasValue)
                    {
                        return Color.Transparent.ToPixel<TPixel>();
                    }

                    PointF intersection = info.Value.Point;

                    Vector4 edgeColor = edge.ColorAt(intersection);

                    float length = DistanceBetween(intersection, this.center);
                    float ratio = length > 0 ? DistanceBetween(intersection, point) / length : 0;

                    var color = Vector4.Lerp(edgeColor, this.centerColor, ratio);

                    return new Color(color).ToPixel<TPixel>();
                }
            }

            private (Edge edge, Intersection? info) FindIntersection(PointF start, PointF end)
            {
                (Edge edge, Intersection? info) closest = default;

                foreach (Edge edge in this.edges)
                {
                    Intersection? intersection = edge.FindIntersection(start, end);

                    if (!intersection.HasValue)
                    {
                        continue;
                    }

                    if (closest.info == null || closest.info.Value.Distance > intersection.Value.Distance)
                    {
                        closest = (edge, intersection);
                    }
                }

                return closest;
            }
        }
    }
}
