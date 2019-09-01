// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        private readonly Polygon path;

        private readonly IList<Edge> edges;

        private readonly Color centerColor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathGradientBrush"/> class.
        /// </summary>
        /// <param name="lines">Line segments of a polygon that represents the gradient area.</param>
        /// <param name="colors">Array of colors that correspond to each point in the polygon.</param>
        /// <param name="centerColor">Color at the center of the gradient area to which the other colors converge.</param>
        public PathGradientBrush(ILineSegment[] lines, Color[] colors, Color centerColor)
        {
            this.path = new Polygon(lines);
            this.centerColor = centerColor;

            Color ColorAt(int index) => colors[index % colors.Length];

            this.edges = this.path.LineSegments
                .Select(s => new Path(s))
                .Select((path, i) => new Edge(path, ColorAt(i), ColorAt(i + 1)))
                .ToList();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathGradientBrush"/> class.
        /// </summary>
        /// <param name="lines">Line segments of a polygon that represents the gradient area.</param>
        /// <param name="colors">Array of colors that correspond to each point in the polygon.</param>
        public PathGradientBrush(ILineSegment[] lines, Color[] colors)
            : this(lines, colors, CalculateCenterColor(colors))
        {
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator<TPixel>(
            ImageFrame<TPixel> source,
            RectangleF region,
            GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return new PathGradientBrushApplicator<TPixel>(source, this.path, this.edges, this.centerColor, options);
        }

        private static Color CalculateCenterColor(Color[] colors) =>
            new Color(colors.Select(c => c.ToVector4()).Aggregate((p1, p2) => p1 + p2) / colors.Length);

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
                this.StartColor = startColor.ToVector4();

                this.End = points.Last();
                this.EndColor = endColor.ToVector4();

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

                return this.buffer
                    .Take(intersections)
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
            private readonly Path path;

            private readonly PointF center;

            private readonly Vector4 centerColor;

            private readonly float maxDistance;

            private readonly IList<Edge> edges;

            /// <summary>
            /// Initializes a new instance of the <see cref="PathGradientBrushApplicator{TPixel}"/> class.
            /// </summary>
            /// <param name="source">The source image.</param>
            /// <param name="path">A polygon that represents the gradient area.</param>
            /// <param name="edges">Edges of the polygon.</param>
            /// <param name="centerColor">Color at the center of the gradient area to which the other colors converge.</param>
            /// <param name="options">The options.</param>
            public PathGradientBrushApplicator(
                ImageFrame<TPixel> source,
                Path path,
                IList<Edge> edges,
                Color centerColor,
                GraphicsOptions options)
                : base(source, options)
            {
                this.path = path;
                this.edges = edges;

                PointF[] points = path.LineSegments.Select(s => s.EndPoint).ToArray();

                this.center = points.Aggregate((p1, p2) => p1 + p2) / points.Length;
                this.centerColor = centerColor.ToVector4();

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

                    if (!this.path.Contains(point))
                    {
                        return Color.Transparent.ToPixel<TPixel>();
                    }

                    Vector2 direction = Vector2.Normalize(point - this.center);

                    PointF end = point + (PointF)(direction * this.maxDistance);

                    (Edge edge, Intersection? info) = this.edges
                        .Select(e => (e, e.FindIntersection(point, end)))
                        .Where(e => e.Item2.HasValue)
                        .Aggregate((min, cur) => min.Item2.Value.Distance > cur.Item2.Value.Distance ? cur : min);

                    PointF intersection = info.Value.Point;

                    Vector4 edgeColor = edge.ColorAt(intersection);

                    float length = DistanceBetween(intersection, this.center);
                    float ratio = length > 0 ? DistanceBetween(intersection, point) / length : 0;

                    Vector4 color = Vector4.Lerp(edgeColor, this.centerColor, ratio);

                    return new Color(color).ToPixel<TPixel>();
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }
        }
    }
}
